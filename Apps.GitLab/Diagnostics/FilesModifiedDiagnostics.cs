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

    internal async Task<DiagnoseEventStreamResponse> CollectEventStreamAsync(int repositoryId, DateTimeOffset timestamp, int minutes)
    {
        var collectedAt = DateTimeOffset.UtcNow;
        var center = timestamp.ToUniversalTime();
        var from = center.AddMinutes(-minutes);
        var to = center.AddMinutes(minutes);
        // Copy the polling event's query so diagnostics do not change its implementation.
        var query = new Dictionary<string, string>
        {
            ["scope"] = "all",
            ["action"] = "pushed",
            ["after"] = from.UtcDateTime.Date.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["before"] = to.UtcDateTime.Date.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["sort"] = "desc",
            ["per_page"] = "100"
        };
        using var deadline = new CancellationTokenSource(MaximumDuration);
        var global = await CollectStreamAsync("/events", query, repositoryId, from, to, deadline.Token);
        var repositoryQuery = new Dictionary<string, string>(query);
        repositoryQuery.Remove("scope");
        var repository = await CollectStreamAsync($"/projects/{repositoryId}/events", repositoryQuery,
            repositoryId, from, to, deadline.Token);
        var comparison = CompareStreams(global, repository, repositoryId);
        var summary = $"Selected repository events in the time window: global feed {global["repository_events_in_window_count"]}, " +
                      $"repository feed {repository["repository_events_in_window_count"]}. " +
                      $"{((JArray)comparison["repository_event_ids_missing_from_global"]!).Count} repository event(s) were absent from the collected global results.";
        if (!_complete) summary += " Evidence collection is partial.";
        var report = new JObject
        {
            ["schema_version"] = 2,
            ["collected_at_utc"] = collectedAt,
            ["collection_finished_at_utc"] = DateTimeOffset.UtcNow,
            ["app_version"] = typeof(FilesModifiedDiagnostics).Assembly.GetName().Version?.ToString(),
            ["base_url"] = client.BaseUrl,
            ["repository_id"] = repositoryId.ToString(CultureInfo.InvariantCulture),
            ["timestamp_with_offset"] = timestamp.ToString("O", CultureInfo.InvariantCulture),
            ["timestamp_utc"] = center,
            ["minutes_before_and_after"] = minutes,
            ["window_from_utc"] = from,
            ["window_to_utc"] = to,
            ["global_stream"] = global,
            ["repository_stream"] = repository,
            ["comparison"] = comparison,
            ["requests"] = _requests,
            ["collection_complete"] = _complete,
            ["warnings"] = _warnings,
            ["limits"] = JObject.FromObject(new
            {
                maximum_pages_per_stream = MaximumEventPages, maximum_reported_events_per_stream = 1000,
                maximum_request_operations = MaximumRequests, maximum_duration_seconds = MaximumDuration.TotalSeconds
            }),
            ["limitations"] = new JArray(
                "The global request copies the polling event query. The repository request uses identical date bounds, action and sort against the selected project's events endpoint, without scope=all.",
                "GitLab filters by date. Each returned record is additionally marked against the exact inclusive UTC timestamp window.",
                "This queries currently visible and retained activity; it cannot reconstruct the original historical API response.",
                "Comparison uses selected-repository event IDs inside the exact window. Missing IDs describe collected results; incomplete scans or reports can omit events.",
                "The supplied window replaces the poller's memory-derived scan window. Branch/file filters, memory, deduplication and Flight creation are not simulated.",
                "Collection completeness describes client retrieval, not a guarantee of exhaustive server-side history. Both streams share the three-minute deadline.",
                "Request operations can include retries under the shared client policy; the deadline includes retries.")
        };
        return new DiagnoseEventStreamResponse
        {
            Summary = summary,
            EventCount = global["events_in_window_count"]!.Value<int>(),
            ReturnedEventCount = global["returned_event_count"]!.Value<int>(),
            GlobalRepositoryEventCount = global["repository_events_in_window_count"]!.Value<int>(),
            RepositoryEventCount = repository["repository_events_in_window_count"]!.Value<int>(),
            RepositoryReturnedEventCount = repository["returned_event_count"]!.Value<int>(),
            CollectionComplete = _complete, ReportJson = report.ToString(Formatting.Indented)
        };
    }

    private async Task<JObject> CollectStreamAsync(string endpoint, Dictionary<string, string> query,
        int repositoryId, DateTimeOffset from, DateTimeOffset to, CancellationToken token)
    {
        const int maximumReportedEvents = 1000;
        var scan = await PagesAsync(endpoint, query, MaximumEventPages, token);
        var evidence = scan.Items.OfType<JObject>().Select(item => StreamEventEvidence(item, from, to)).ToList();
        var inWindow = evidence.Count(item => item["in_requested_window"]?.Value<bool?>() == true);
        var repositoryInWindow = evidence.Count(item => item["in_requested_window"]?.Value<bool?>() == true &&
                                                       Number(item, "project_id") == repositoryId);
        var unknownTime = evidence.Count(item => item["in_requested_window"]?.Type == JTokenType.Null);
        var unknownId = evidence.Count(item => Number(item, "id") is not > 0 || Number(item, "project_id") is not > 0);
        var complete = scan.Complete && unknownTime == 0 && unknownId == 0 &&
                       evidence.Count == scan.Items.Count && evidence.Count <= maximumReportedEvents;
        if (unknownTime > 0) Warn($"{endpoint}: {unknownTime} event(s) had missing or invalid timestamps; window membership is unknown.");
        if (unknownId > 0) Warn($"{endpoint}: {unknownId} event(s) had missing or invalid event/repository IDs; comparison is incomplete.");
        if (evidence.Count != scan.Items.Count) Warn($"{endpoint}: non-object records could not be inspected.");
        if (evidence.Count > maximumReportedEvents)
            Warn($"{endpoint}: only {maximumReportedEvents} of {evidence.Count} events are reported; selected-repository events inside the window take priority.");
        var reported = evidence.Count <= maximumReportedEvents ? evidence : evidence
            .OrderByDescending(item => item["in_requested_window"]?.Value<bool?>() == true && Number(item, "project_id") == repositoryId)
            .ThenByDescending(item => item["in_requested_window"]?.Value<bool?>() == true)
            .Take(maximumReportedEvents).ToList();
        return new JObject
        {
            ["endpoint"] = endpoint, ["query"] = JObject.FromObject(query),
            ["pages_scanned"] = scan.Pages, ["pagination_complete"] = scan.Complete,
            ["returned_event_count"] = scan.Items.Count, ["events_in_window_count"] = inWindow,
            ["repository_events_in_window_count"] = repositoryInWindow,
            ["events_with_unknown_timestamp_count"] = unknownTime, ["reported_event_count"] = reported.Count,
            ["collection_complete"] = complete, ["events"] = new JArray(reported)
        };
    }

    internal static JObject CompareStreams(JObject global, JObject repository, int repositoryId)
    {
        var globalIds = WindowEvents(global, repositoryId).Select(item => Text(item, "id"))
            .Where(id => id is not null).ToHashSet();
        var missingIds = WindowEvents(repository, repositoryId).Select(item => Text(item, "id"))
            .Where(id => id is not null && !globalIds.Contains(id)).Distinct().ToList();
        return new JObject
        {
            ["repository_event_ids_missing_from_global"] = new JArray(missingIds),
            ["complete"] = global["collection_complete"]?.Value<bool?>() == true &&
                             repository["collection_complete"]?.Value<bool?>() == true
        };
    }

    private static IEnumerable<JObject> WindowEvents(JObject stream, int repositoryId) =>
        ((JArray)stream["events"]!).OfType<JObject>().Where(item =>
            item["in_requested_window"]?.Value<bool?>() == true && Number(item, "project_id") == repositoryId);

    internal static JObject StreamEventEvidence(JObject item, DateTimeOffset from, DateTimeOffset to)
    {
        var evidence = Pick(item, "id", "project_id", "action_name", "author_id", "created_at", "push_data");
        var createdAt = Date(item, "created_at");
        evidence["in_requested_window"] = createdAt.HasValue
            ? JToken.FromObject(createdAt.Value >= from && createdAt.Value <= to) : JValue.CreateNull();
        evidence["poller_payload_issues"] = SkipReasons(item, item["push_data"] as JObject);
        return evidence;
    }

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
        var streamTimestamp = LatestRelatedPushTimestamp(candidates.Take(MaximumCandidates), branch);
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
            ["event_stream_inputs"] = new JObject
            {
                ["repository_id"] = repositoryId.ToString(CultureInfo.InvariantCulture),
                ["timestamp"] = streamTimestamp,
                ["timestamp_source"] = streamTimestamp is null
                    ? "no_confirmed_push_timestamp_on_expected_branch" : "latest_confirmed_push_on_expected_branch"
            },
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
            RepositoryId = repositoryId.ToString(CultureInfo.InvariantCulture), Timestamp = streamTimestamp,
            Summary = summary, RelatedPushCount = related, CollectionComplete = _complete,
            ReportJson = report.ToString(Formatting.Indented)
        };
    }

    internal static string? LatestRelatedPushTimestamp(IEnumerable<JObject> events, string branch) => events
        .Where(item => Text(item, "target_commit_correlation") is "push_tip" or "push_comparison" &&
                       Text(item["push_data"] as JObject, "ref") == branch)
        .Select(item => Date(item, "created_at"))
        .Where(timestamp => timestamp.HasValue)
        .OrderByDescending(timestamp => timestamp)
        .FirstOrDefault()?.ToString("O", CultureInfo.InvariantCulture);

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

    private static DateTimeOffset? Date(JObject? source, string field)
    {
        // Preserve fractional seconds when Newtonsoft has already parsed an ISO timestamp as a date value.
        var value = (source?[field] as JValue)?.Value;
        if (value is DateTimeOffset offset) return offset.ToUniversalTime();
        if (value is DateTime time)
            return new DateTimeOffset(time.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : time).ToUniversalTime();
        return DateTimeOffset.TryParse(value as string, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date) ? date : null;
    }
}
