using System.Diagnostics;
using System.Globalization;
using System.Net;
using Apps.Gitlab;
using Apps.GitLab.Models.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Apps.GitLab.Diagnostics;

internal sealed class FilesModifiedDiagnostics(BlackbirdGitlabClient client)
{
    private const int MaximumRequests = 150;
    private const int MaximumEventPages = 20;
    private const int MaximumDetailPages = 10;
    private const int MaximumCandidates = 30;
    private const int MaximumReportedEvents = 200;
    private const int MaximumPushCommits = 50;
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromMinutes(3);
    private readonly JArray _requests = [];
    private readonly JArray _scans = [];
    private readonly JArray _warnings = [];
    private readonly Dictionary<string, JObject> _diffs = new(StringComparer.OrdinalIgnoreCase);
    private bool _complete = true;

    internal async Task<DiagnoseFilesModifiedResponse> CollectAsync(
        int repositoryId, string sha, string branch, DateTimeOffset? collectionTime = null)
    {
        var now = (collectionTime ?? DateTimeOffset.UtcNow).ToUniversalTime();
        using var deadline = new CancellationTokenSource(MaximumDuration);
        var token = deadline.Token;
        var root = $"/projects/{repositoryId}";
        var commitRoot = $"{root}/repository/commits/{Uri.EscapeDataString(sha)}";
        var project = await GetAsync(root, token);
        var commit = await GetAsync(commitRoot, token);
        var branchData = await GetAsync($"{root}/repository/branches/{Uri.EscapeDataString(branch)}", token);
        var refs = await PagesAsync(commitRoot + "/refs", new() { ["type"] = "branch" }, MaximumDetailPages, token);
        var branchFound = refs.Items.OfType<JObject>().Any(item => Text(item, "name") == branch);
        bool? reachable = branchFound ? true : refs.Complete ? false : null;
        var targetDiff = await DiffAsync(root, sha, token);

        var windows = new List<(DateTimeOffset From, DateTimeOffset To)> { (now.AddDays(-7), now) };
        var commitTime = Date(commit, "committed_date");
        if (commitTime.HasValue && commitTime.Value < now.AddDays(-7))
        {
            var from = commitTime.Value.ToUniversalTime().AddDays(-2);
            var to = commitTime.Value.ToUniversalTime().AddDays(2);
            if (to >= windows[0].From)
                windows[0] = (from, now);
            else
                windows.Add((from, to));
        }

        var events = new Dictionary<string, JObject>(StringComparer.Ordinal);
        foreach (var window in windows)
        {
            foreach (var source in new[] { "project", "global" })
            {
                var query = new Dictionary<string, string>
                {
                    ["action"] = "pushed", ["sort"] = "desc",
                    ["after"] = window.From.UtcDateTime.Date.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["before"] = window.To.UtcDateTime.Date.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                };
                if (source == "global") query["scope"] = "all";
                var scan = await PagesAsync(source == "global" ? "/events" : root + "/events",
                    query, MaximumEventPages, token);
                _scans.Add(new JObject
                {
                    ["source"] = source, ["from_utc"] = window.From, ["to_utc"] = window.To,
                    ["pages_scanned"] = scan.Pages, ["complete"] = scan.Complete,
                    ["returned_event_count"] = scan.Items.Count
                });
                foreach (var item in scan.Items.OfType<JObject>())
                {
                    if (Number(item, "project_id") != repositoryId) continue;
                    var at = Date(item, "created_at");
                    if (at.HasValue && (at < window.From || at > window.To)) continue;
                    // Keep incomplete events as evidence, including events with no ID or timestamp.
                    var key = Text(item, "id") ?? item.ToString(Formatting.None);
                    if (!events.TryGetValue(key, out var evidence))
                    {
                        evidence = Pick(item, "id", "project_id", "action_name", "author_id", "created_at", "push_data");
                        evidence["sources"] = new JArray();
                        evidence["enrichment_status"] = "not_selected_or_limit_reached";
                        events.Add(key, evidence);
                    }
                    var sources = (JArray)evidence["sources"]!;
                    if (!sources.Values<string>().Contains(source)) sources.Add(source);
                }
            }
        }

        var candidates = events.Values
            .Where(item => item["push_data"] is not JObject data ||
                           string.IsNullOrWhiteSpace(Text(data, "ref")) ||
                           Text(data, "ref") == branch || SameSha(Text(data, "commit_to"), sha))
            .OrderByDescending(item => SameSha(Text(item["push_data"] as JObject, "commit_to"), sha))
            .ThenByDescending(item => Date(item, "created_at"))
            .ToList();
        if (candidates.Count > MaximumCandidates)
            Warn($"Only {MaximumCandidates} of {candidates.Count} candidate pushes were enriched.");

        var related = 0;
        foreach (var item in candidates.Take(MaximumCandidates))
        {
            item["enrichment_status"] = "attempted";
            var data = item["push_data"] as JObject;
            var from = Text(data, "commit_from");
            var to = Text(data, "commit_to");
            var direct = SameSha(to, sha);
            item["expected_branch_matches"] = Text(data, "ref") is { } eventBranch
                ? JToken.FromObject(eventBranch == branch) : JValue.CreateNull();
            item["poller_skip_reasons"] = SkipReasons(item, data);
            item["target_commit_correlation"] = direct ? "push_tip" : "unknown";
            item["push_commits"] = new JArray();
            if (UsableSha(from) && UsableSha(to))
            {
                var comparison = await GetAsync(root + "/repository/compare", token,
                    new() { ["from"] = from!, ["to"] = to!, ["straight"] = "true" });
                item["comparison"] = Pick(comparison, "compare_timeout", "compare_same_ref");
                var commits = comparison?["commits"] as JArray;
                if (commits is null || commits.Count == 0)
                {
                    Warn($"Push event {Text(item, "id") ?? "unknown"}: comparison commits unavailable or empty.");
                }
                else
                {
                    var containsTarget = commits.OfType<JObject>().Any(c => SameSha(Text(c, "id"), sha));
                    if (containsTarget) item["target_commit_correlation"] = direct ? "push_tip" : "push_comparison";
                    else if (!direct && comparison?["compare_timeout"]?.Value<bool?>() == false)
                        item["target_commit_correlation"] = "not_in_returned_comparison";
                    if (comparison?["compare_timeout"]?.Value<bool?>() != false)
                        Warn($"Push event {Text(item, "id") ?? "unknown"}: comparison completeness is unknown or timed out.");
                    var commitEvidence = (JArray)item["push_commits"]!;
                    foreach (var pushCommit in commits.OfType<JObject>().Take(MaximumPushCommits))
                    {
                        var entry = Pick(pushCommit, "id", "message", "authored_date", "committed_date");
                        // Collect all commits' paths for confirmed pushes, not just the target commit.
                        if (direct || containsTarget)
                        {
                            var commitSha = Text(pushCommit, "id");
                            if (UsableSha(commitSha)) entry["changed_files"] = await DiffAsync(root, commitSha!, token);
                            else Warn("A comparison commit had no usable SHA; its changed files could not be collected.");
                        }
                        commitEvidence.Add(entry);
                    }
                    item["returned_commit_count"] = commits.Count;
                    item["commits_collected"] = Math.Min(commits.Count, MaximumPushCommits);
                    if (commits.Count > MaximumPushCommits) Warn($"Push event {Text(item, "id")}: commit evidence capped at {MaximumPushCommits}.");
                }
            }
            if (item["target_commit_correlation"]!.Value<string>() is "push_tip" or "push_comparison") related++;
        }

        var reportedEvents = candidates.Take(MaximumCandidates)
            .Concat(events.Values).Distinct().Take(MaximumReportedEvents).ToList();
        if (events.Count > MaximumReportedEvents)
            Warn($"Only {MaximumReportedEvents} of {events.Count} repository events are included in the report; enriched candidates take priority.");
        var summary = related > 0
            ? $"Found {related} push event(s) containing the commit. Review the JSON evidence against the event filters and historical polling logs."
            : "No related push was confirmed within the scanned ranges. Review candidate events, access results and collection limits.";
        if (!_complete) summary += " Evidence collection is partial.";
        var report = new JObject
        {
            ["schema_version"] = 1,
            ["collected_at_utc"] = now,
            ["collection_finished_at_utc"] = DateTimeOffset.UtcNow,
            ["app_version"] = typeof(FilesModifiedDiagnostics).Assembly.GetName().Version?.ToString(),
            ["base_url"] = client.BaseUrl,
            ["repository_id"] = repositoryId.ToString(CultureInfo.InvariantCulture),
            ["commit_sha"] = sha, ["expected_branch"] = branch,
            ["repository"] = Pick(project, "id", "path_with_namespace", "default_branch"),
            ["commit"] = Pick(commit, "id", "parent_ids", "title", "message", "authored_date", "committed_date"),
            ["changed_files"] = targetDiff,
            ["branch"] = new JObject
            {
                ["name"] = Text(branchData, "name"), ["head_sha"] = branchData?["commit"]?["id"]?.DeepClone(),
                ["target_commit_reachable_now"] = reachable.HasValue ? JToken.FromObject(reachable.Value) : JValue.CreateNull(),
                ["commit_branch_refs"] = new JArray(refs.Items.OfType<JObject>().Select(r => Pick(r, "type", "name"))),
                ["refs_complete"] = refs.Complete
            },
            ["event_scans"] = _scans,
            ["repository_push_events"] = new JArray(reportedEvents),
            ["repository_event_count"] = events.Count,
            ["candidate_count"] = candidates.Count,
            ["related_push_count"] = related,
            ["requests"] = _requests,
            ["collection_complete"] = _complete,
            ["warnings"] = _warnings,
            ["limits"] = JObject.FromObject(new
            {
                recent_days = 7, commit_hint_days_each_side = 2, maximum_request_operations = MaximumRequests,
                maximum_duration_seconds = MaximumDuration.TotalSeconds, maximum_event_pages_per_feed_per_window = MaximumEventPages,
                maximum_detail_pages = MaximumDetailPages, page_size = 100,
                maximum_candidates_enriched = MaximumCandidates, maximum_commits_per_push = MaximumPushCommits,
                maximum_reported_events = MaximumReportedEvents
            }),
            ["limitations"] = new JArray(
                "Commit time is not push time. Searches cover only the reported ranges and currently retained, visible activity.",
                "Missing events or a commit absent from a comparison do not prove that a push never occurred.",
                "Branch reachability and feed visibility describe collection time, not the historical poll.",
                "Each recorded request operation can include retries under the shared client policy; the deadline covers retries too.",
                "Event filters, historical polling memory, baseline resets, deduplication and platform Flight creation are not evaluated.",
                "Poller skip reasons describe payload completeness only; unknown ref_count alone is not a skip reason.",
                "The poller accepts only modified files; added, deleted and renamed files do not qualify. Diff patches and file contents are omitted.")
        };
        return new DiagnoseFilesModifiedResponse
        {
            Summary = summary, RelatedPushCount = related, CollectionComplete = _complete,
            ReportJson = report.ToString(Formatting.Indented)
        };
    }

    private async Task<JObject> DiffAsync(string root, string sha, CancellationToken token)
    {
        if (_diffs.TryGetValue(sha, out var cached)) return (JObject)cached.DeepClone();
        var pages = await PagesAsync(root + $"/repository/commits/{Uri.EscapeDataString(sha)}/diff",
            [], MaximumDetailPages, token);
        var files = new JArray(pages.Items.OfType<JObject>().Select(diff =>
        {
            var evidence = Pick(diff, "old_path", "new_path", "new_file", "deleted_file", "renamed_file", "collapsed", "too_large");
            var missingFlags = new[] { "new_file", "deleted_file", "renamed_file" }
                .Any(field => diff[field]?.Type != JTokenType.Boolean);
            evidence["modified_in_place"] = missingFlags ? JValue.CreateNull() : JToken.FromObject(
                diff["new_file"]!.Value<bool>() == false && diff["deleted_file"]!.Value<bool>() == false &&
                diff["renamed_file"]!.Value<bool>() == false && !string.IsNullOrWhiteSpace(Text(diff, "new_path")));
            evidence["poller_would_reject_incomplete_diff"] = missingFlags || string.IsNullOrWhiteSpace(Text(diff, "new_path"));
            return evidence;
        }));
        var result = new JObject { ["files"] = files, ["pagination_complete"] = pages.Complete };
        result["has_collapsed_or_too_large"] = files.OfType<JObject>().Any(f =>
            f["collapsed"]?.Value<bool?>() == true || f["too_large"]?.Value<bool?>() == true);
        _diffs[sha] = result;
        return (JObject)result.DeepClone();
    }

    private async Task<JObject?> GetAsync(string path, CancellationToken token, Dictionary<string, string>? query = null)
    {
        var response = await ReadAsync(path, query ?? [], token);
        if (response is null) return null;
        try { return JObject.Parse(response.Content ?? string.Empty); }
        catch (JsonException) { Warn($"GET {path}: expected a JSON object."); return null; }
    }

    private async Task<(JArray Items, bool Complete, int Pages)> PagesAsync(
        string path, Dictionary<string, string> query, int maximumPages, CancellationToken token)
    {
        var items = new JArray();
        for (var page = 1; page <= maximumPages; page++)
        {
            var parameters = new Dictionary<string, string>(query)
            {
                ["page"] = page.ToString(CultureInfo.InvariantCulture), ["per_page"] = "100"
            };
            var response = await ReadAsync(path, parameters, token);
            if (response is null) return (items, false, page - 1);
            JArray batch;
            try { batch = JArray.Parse(response.Content ?? string.Empty); }
            catch (JsonException) { Warn($"GET {path}: expected a JSON array."); return (items, false, page); }
            foreach (var item in batch) items.Add(item.DeepClone());
            var nextHeader = response.Headers?.FirstOrDefault(h =>
                string.Equals(h.Name, "X-Next-Page", StringComparison.OrdinalIgnoreCase));
            var next = nextHeader?.Value?.ToString();
            var links = string.Join(",", response.Headers?.Where(h =>
                string.Equals(h.Name, "Link", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value?.ToString()) ?? []);
            // Rebuild pages on the original endpoint; never follow a response URL with credentials.
            if (nextHeader is not null)
            {
                if (string.IsNullOrWhiteSpace(next)) return (items, true, page);
                if (next != (page + 1).ToString(CultureInfo.InvariantCulture))
                {
                    Warn($"GET {path}: unexpected pagination cursor; remaining pages were not read.");
                    return (items, false, page);
                }
            }
            else if (!links.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase) &&
                     !links.Contains("rel=next", StringComparison.OrdinalIgnoreCase) && batch.Count < 100)
                return (items, true, page);
        }
        Warn($"GET {path}: page limit of {maximumPages} reached; earlier pages are preserved.");
        return (items, false, maximumPages);
    }

    private async Task<RestResponse?> ReadAsync(string path, Dictionary<string, string> query, CancellationToken token)
    {
        if (_requests.Count >= MaximumRequests || token.IsCancellationRequested)
        {
            Warn("Request budget or three-minute deadline reached; remaining evidence was not collected.");
            return null;
        }
        var request = client.CreateRequest(path, Method.Get);
        foreach (var parameter in query) request.AddQueryParameter(parameter.Key, parameter.Value);
        var evidence = new JObject { ["method"] = "GET", ["path"] = path, ["query"] = JObject.FromObject(query) };
        _requests.Add(evidence);
        var timer = Stopwatch.StartNew();
        try
        {
            // Preserve HTTP failures in the report instead of losing all evidence to an exception.
            var response = await client.ExecuteWithErrorHandling(request, token, Enum.GetValues<HttpStatusCode>());
            evidence["status_code"] = (int)response.StatusCode;
            evidence["request_id"] = response.Headers?.FirstOrDefault(h =>
                string.Equals(h.Name, "X-Request-Id", StringComparison.OrdinalIgnoreCase))?.Value?.ToString();
            if (response.IsSuccessStatusCode) return response;
            evidence["error"] = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Authentication rejected; check the selected connection.",
                HttpStatusCode.Forbidden => "Access forbidden; check connection permissions and instance access rules.",
                HttpStatusCode.NotFound => "Not found or not visible to this connection.",
                _ => "GitLab returned an unsuccessful HTTP response."
            };
            Warn($"GET {path}: HTTP {(int)response.StatusCode}.");
        }
        catch (OperationCanceledException)
        {
            evidence["error"] = "Collection deadline reached.";
            Warn("Collection deadline reached; partial evidence is preserved.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Exception messages and error response bodies can contain credentials or HTML from a proxy.
            evidence["error"] = "Request failed before a usable response was received.";
            Warn($"GET {path}: request failed; check connectivity from the execution environment.");
        }
        finally { evidence["elapsed_ms"] = timer.ElapsedMilliseconds; }
        return null;
    }

    private void Warn(string message)
    {
        _complete = false;
        if (!_warnings.Values<string>().Contains(message)) _warnings.Add(message);
    }

    private static JObject Pick(JObject? source, params string[] fields)
        => new(fields.Select(field => new JProperty(field, source?[field]?.DeepClone())));

    private static string? Text(JObject? source, string field) => source?[field]?.Value<string>();
    private static bool SameSha(string? left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static bool UsableSha(string? value) => !string.IsNullOrWhiteSpace(value) && !value.All(c => c == '0');

    internal static JArray SkipReasons(JObject item, JObject? data)
    {
        var reasons = new JArray();
        if (Number(item, "id") is not > 0) reasons.Add("Missing or invalid event ID");
        if (Date(item, "created_at") is null) reasons.Add("Missing or invalid push timestamp");
        if (data is null) { reasons.Add("Missing push_data"); return reasons; }
        if (Number(data, "commit_count") is not > 0) reasons.Add("Missing or non-positive commit_count");
        if (Number(data, "ref_count") > 3) reasons.Add("ref_count exceeds the poller's bulk threshold of 3");
        if (Text(data, "action") != "pushed") reasons.Add("action is not pushed");
        if (Text(data, "ref_type") != "branch") reasons.Add("ref_type is not branch");
        if (string.IsNullOrWhiteSpace(Text(data, "ref"))) reasons.Add("Missing branch ref");
        if (!UsableSha(Text(data, "commit_from"))) reasons.Add("Missing or zero commit_from");
        if (!UsableSha(Text(data, "commit_to"))) reasons.Add("Missing or zero commit_to");
        if (Number(item, "author_id") is null) reasons.Add("Missing pusher author_id");
        return reasons;
    }

    private static long? Number(JObject? source, string field) =>
        long.TryParse(Text(source, field), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static DateTimeOffset? Date(JObject? source, string field) =>
        DateTimeOffset.TryParse(Text(source, field), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date) ? date : null;
}
