using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Apps.Gitlab;
using Apps.Gitlab.Constants;
using Apps.GitLab.Polling.Models;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Polling;
using Microsoft.Extensions.FileSystemGlobbing;
using Newtonsoft.Json;
using RestSharp;

namespace Apps.GitLab.Polling;

[PollingEventList("Pushes")]
public class PushPollingList : BaseInvocable
{
    private const int EnrichmentConcurrency = 4;
    private const int EventPageSafetyLimit = 200;
    private const int DiffPageSafetyLimit = 100;
    private const int BulkPushRefThreshold = 3;
    private static readonly TimeSpan Overlap = TimeSpan.FromHours(24);
    private static readonly TimeSpan MaximumScanDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    private readonly BlackbirdGitlabClient _client;
    private readonly Func<DateTimeOffset> _utcNow;

    public PushPollingList(InvocationContext invocationContext)
        : this(invocationContext, new BlackbirdGitlabClient(
            invocationContext.AuthenticationCredentialsProviders), () => DateTimeOffset.UtcNow)
    {
    }

    internal PushPollingList(
        InvocationContext invocationContext,
        BlackbirdGitlabClient client,
        Func<DateTimeOffset> utcNow) : base(invocationContext)
    {
        _client = client;
        _utcNow = utcNow;
    }

    [PollingEvent(
        "On files modified across repositories",
        Description = "Triggered at specified intervals when modified files in selected repositories match configured filters")]
    public async Task<PollingEventResponse<PushPollingMemory, FilesModifiedPollingResponse>>
        OnFilesModifiedAcrossRepositories(
            PollingEventRequest<PushPollingMemory> request,
            [PollingEventParameter] FilesModifiedPollingInput input)
    {
        static string[] Normalize(IEnumerable<string>? values) =>
            values?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray() ?? [];

        var repositoryValues = Normalize(input.RepositoryIds).Select(value => value.Trim()).ToArray();
        if (repositoryValues.Length == 0)
            throw new PluginMisconfigurationException("Select at least one repository to include.");

        var repositoryIds = new HashSet<int>();
        foreach (var repositoryValue in repositoryValues)
        {
            if (!int.TryParse(repositoryValue, NumberStyles.None, CultureInfo.InvariantCulture, out var repositoryId) ||
                repositoryId <= 0)
                throw new PluginMisconfigurationException($"Invalid repository ID: {repositoryValue}");
            repositoryIds.Add(repositoryId);
        }

        var pusherValues = Normalize(input.PusherUserIds).Select(value => value.Trim()).ToArray();
        var ignoredPusherValues = Normalize(input.IgnoredPusherUserIds).Select(value => value.Trim()).ToArray();
        var pusherIds = new HashSet<int>();
        var ignoredPusherIds = new HashSet<int>();
        foreach (var (values, target, label) in new[]
                 {
                     (pusherValues, pusherIds, "pusher"),
                     (ignoredPusherValues, ignoredPusherIds, "ignored pusher")
                 })
        {
            foreach (var value in values)
            {
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0)
                    throw new PluginMisconfigurationException($"Invalid {label} user ID: {value}");
                target.Add(userId);
            }
        }

        var branchPatterns = Normalize(input.BranchPatterns);
        var ignoredBranchPatterns = Normalize(input.IgnoredBranchPatterns);
        var filePatterns = Normalize(input.FilePatterns);
        var includedMessagePatterns = Normalize(input.IncludedCommitMessagePatterns);
        var excludedMessagePatterns = Normalize(input.ExcludedCommitMessagePatterns);
        var branchRegexes = BuildRegexes(branchPatterns, "branch watch");
        var ignoredBranchRegexes = BuildRegexes(ignoredBranchPatterns, "branch ignore");
        var includedMessageRegexes = BuildRegexes(includedMessagePatterns, "commit message include");
        var excludedMessageRegexes = BuildRegexes(excludedMessagePatterns, "commit message exclude");

        var fileMatchers = new List<Matcher>();
        try
        {
            foreach (var filePattern in filePatterns)
            {
                var matcher = new Matcher(StringComparison.Ordinal);
                matcher.AddInclude(filePattern.Contains('/') ? filePattern : $"**/{filePattern}");
                fileMatchers.Add(matcher);
            }
        }
        catch (ArgumentException exception)
        {
            throw new PluginMisconfigurationException($"Invalid file glob: {exception.Message}");
        }

        var fingerprintJson = JsonConvert.SerializeObject(new
        {
            Repositories = repositoryIds.Order().ToArray(),
            Branches = branchPatterns,
            IgnoredBranches = ignoredBranchPatterns,
            Files = filePatterns,
            IncludedMessages = includedMessagePatterns,
            ExcludedMessages = excludedMessagePatterns,
            Pushers = pusherIds.Order().ToArray(),
            IgnoredPushers = ignoredPusherIds.Order().ToArray()
        });
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintJson)));

        var scanStopwatch = Stopwatch.StartNew();
        using var scanDeadline = new CancellationTokenSource(MaximumScanDuration);
        var scanUpperUtc = _utcNow().ToUniversalTime();
        var isBaseline = request.Memory?.LastCompletedScanUtc is null ||
                         request.Memory.LastCompletedScanUtc.Value > scanUpperUtc ||
                         !string.Equals(
                             request.Memory.InputConfigurationFingerprint,
                             fingerprint,
                             StringComparison.Ordinal);
        var scanLowerUtc = isBaseline
            ? scanUpperUtc - Overlap
            : request.Memory!.LastCompletedScanUtc!.Value.ToUniversalTime() - Overlap;

        var eventsRequest = _client.CreateRequest("/events", Method.Get);
        eventsRequest.AddQueryParameter("scope", "all");
        eventsRequest.AddQueryParameter("action", "pushed");
        eventsRequest.AddQueryParameter(
            "after",
            scanLowerUtc.UtcDateTime.Date.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        eventsRequest.AddQueryParameter(
            "before",
            scanUpperUtc.UtcDateTime.Date.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        eventsRequest.AddQueryParameter("sort", "desc");
        eventsRequest.AddQueryParameter("per_page", 100);

        var events = await _client.ExecutePaginatedWithErrorHandling<GitLabPushEvent>(
            eventsRequest,
            scanDeadline.Token,
            maximumPages: EventPageSafetyLimit,
            maximumDuration: MaximumScanDuration);
        EnsureWithinScanLimit(scanStopwatch);

        var selectedEvents = new List<(ProcessedPushEventKey Key, GitLabPushEvent Event)>();
        var selectedEventKeys = new HashSet<ProcessedPushEventKey>();
        foreach (var pushEvent in events)
        {
            if (!repositoryIds.Contains(pushEvent.ProjectId))
                continue;
            if (pushEvent.Id <= 0 || pushEvent.CreatedAt is null)
                throw new PluginApplicationException(
                    $"GitLab returned an incomplete event for selected repository {pushEvent.ProjectId}.");

            var createdAt = pushEvent.CreatedAt.Value.ToUniversalTime();
            if (createdAt < scanLowerUtc || createdAt > scanUpperUtc)
                continue;

            var key = new ProcessedPushEventKey
            {
                ProjectId = pushEvent.ProjectId,
                EventId = pushEvent.Id,
                CreatedAt = createdAt
            };
            if (selectedEventKeys.Add(key))
                selectedEvents.Add((key, pushEvent));
        }

        if (isBaseline)
        {
            var completedAt = _utcNow().ToUniversalTime();
            return new PollingEventResponse<PushPollingMemory, FilesModifiedPollingResponse>
            {
                FlyBird = false,
                Result = null!,
                Memory = new PushPollingMemory
                {
                    LastCompletedScanUtc = completedAt,
                    InputConfigurationFingerprint = fingerprint,
                    RecentProcessedKeys = selectedEvents
                        .Select(item => item.Key)
                        .Where(key => key.CreatedAt >= completedAt - Overlap)
                        .OrderBy(key => key.CreatedAt)
                        .ThenBy(key => key.ProjectId)
                        .ThenBy(key => key.EventId)
                        .ToList()
                }
            };
        }

        var previousKeys = (request.Memory!.RecentProcessedKeys ?? [])
            .ToHashSet();
        var unseenEvents = selectedEvents
            .Where(item => !previousKeys.Contains(item.Key))
            .Select((item, index) => (item.Event, Index: index))
            .ToList();
        var matchingPushes = new ConcurrentBag<(int Index, PushPollingOutput Push)>();

        await Parallel.ForEachAsync(
            unseenEvents,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = EnrichmentConcurrency,
                CancellationToken = scanDeadline.Token
            },
            async (candidate, cancellationToken) =>
            {
                EnsureWithinScanLimit(scanStopwatch);
                var pushEvent = candidate.Event;
                var pushData = pushEvent.PushData;
                if (pushData is null ||
                    pushData.CommitCount <= 0 ||
                    pushData.RefCount > BulkPushRefThreshold ||
                    !string.Equals(pushData.Action, "pushed", StringComparison.Ordinal) ||
                    !string.Equals(pushData.RefType, "branch", StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(pushData.Ref) ||
                    string.IsNullOrWhiteSpace(pushData.CommitFrom) ||
                    string.IsNullOrWhiteSpace(pushData.CommitTo) ||
                    pushData.CommitFrom.All(character => character == '0') ||
                    pushData.CommitTo.All(character => character == '0') ||
                    pushEvent.AuthorId is null)
                {
                    InvocationContext.Logger?.LogWarning(
                        "Skipping incomplete GitLab push event {0} for project {1}; ref count {2}, supported bulk threshold {3}.",
                        [pushEvent.Id,
                            pushEvent.ProjectId,
                            pushData?.RefCount?.ToString(CultureInfo.InvariantCulture) ?? "unknown",
                            BulkPushRefThreshold]);
                    return;
                }

                if (MatchesAny(ignoredBranchRegexes, pushData.Ref) ||
                    (branchRegexes.Length > 0 && !MatchesAny(branchRegexes, pushData.Ref)))
                    return;

                var pusherId = pushEvent.AuthorId.Value;
                if (ignoredPusherIds.Contains(pusherId) ||
                    (pusherIds.Count > 0 && !pusherIds.Contains(pusherId)))
                    return;

                EnsureWithinScanLimit(scanStopwatch);
                var compareRequest = _client.CreateRequest(
                    $"/projects/{pushEvent.ProjectId}/repository/compare",
                    Method.Get);
                compareRequest.AddQueryParameter("from", pushData.CommitFrom);
                compareRequest.AddQueryParameter("to", pushData.CommitTo);
                compareRequest.AddQueryParameter("straight", "true");
                var compareHttpResponse = await _client.ExecuteWithErrorHandling(
                    compareRequest,
                    cancellationToken,
                    HttpStatusCode.BadRequest,
                    HttpStatusCode.NotFound);
                if (!compareHttpResponse.IsSuccessStatusCode)
                {
                    var compareError = compareHttpResponse.ErrorMessage ?? compareHttpResponse.Content ?? string.Empty;
                    if (compareError.Contains("Commit Not Found", StringComparison.OrdinalIgnoreCase))
                    {
                        InvocationContext.Logger?.LogWarning(
                            "Skipping unrecoverable GitLab push event {0} for project {1}; compare returned {2}.",
                            [pushEvent.Id, pushEvent.ProjectId, (int)compareHttpResponse.StatusCode]);
                        return;
                    }

                    throw new PluginApplicationException(
                        $"{(int)compareHttpResponse.StatusCode}: {compareError}");
                }

                var compare = JsonConvert.DeserializeObject<GitLabCompareResponse>(
                                  compareHttpResponse.Content ?? string.Empty,
                                  JsonConfig.JsonSettings)
                              ?? throw new PluginApplicationException(
                                  $"GitLab returned an invalid comparison for project {pushEvent.ProjectId}.");
                var commits = compare.Commits ?? [];
                if (commits.Count == 0)
                    throw new PluginApplicationException(
                        $"GitLab comparison for push event {pushEvent.Id} contained no commits.");

                var matchedPaths = new List<string>();
                var seenPaths = new HashSet<string>(StringComparer.Ordinal);
                var incompleteDiff = false;
                foreach (var commit in commits)
                {
                    EnsureWithinScanLimit(scanStopwatch);
                    if (string.IsNullOrWhiteSpace(commit.Id))
                        throw new PluginApplicationException(
                            $"GitLab returned a comparison commit without an ID for project {pushEvent.ProjectId}.");
                    var commitMessage = commit.Message ?? string.Empty;
                    if (MatchesAny(excludedMessageRegexes, commitMessage) ||
                        (includedMessageRegexes.Length > 0 &&
                          !MatchesAny(includedMessageRegexes, commitMessage)))
                        continue;

                    var diffRequest = _client.CreateRequest(
                        $"/projects/{pushEvent.ProjectId}/repository/commits/{Uri.EscapeDataString(commit.Id)}/diff",
                        Method.Get);
                    diffRequest.AddQueryParameter("per_page", 100);
                    var diffs = await _client.ExecutePaginatedWithErrorHandling<GitLabCommitDiff>(
                        diffRequest,
                        cancellationToken,
                        DiffPageSafetyLimit,
                        MaximumScanDuration - scanStopwatch.Elapsed);

                    if (diffs.Any(diff => diff.Collapsed == true || diff.TooLarge == true))
                    {
                        incompleteDiff = true;
                        break;
                    }

                    if (diffs.Any(diff =>
                            string.IsNullOrWhiteSpace(diff.NewPath) ||
                            diff.NewFile is null ||
                            diff.DeletedFile is null ||
                            diff.RenamedFile is null))
                        throw new PluginApplicationException(
                            $"GitLab returned an incomplete diff for commit {commit.Id} in project {pushEvent.ProjectId}.");

                    foreach (var diff in diffs.Where(diff =>
                                 diff.NewFile == false && diff.DeletedFile == false && diff.RenamedFile == false &&
                                 !string.IsNullOrWhiteSpace(diff.NewPath)))
                    {
                        if ((fileMatchers.Count == 0 ||
                             fileMatchers.Any(matcher => matcher.Match(diff.NewPath).HasMatches)) &&
                            seenPaths.Add(diff.NewPath))
                            matchedPaths.Add(diff.NewPath);
                    }
                }

                if (incompleteDiff)
                {
                    InvocationContext.Logger?.LogWarning(
                        "Skipping GitLab push event {0} for project {1}; commit diff was collapsed or too large.",
                        [pushEvent.Id, pushEvent.ProjectId]);
                    return;
                }
                if (matchedPaths.Count == 0)
                    return;

                matchingPushes.Add((candidate.Index, new PushPollingOutput
                {
                    RepositoryId = pushEvent.ProjectId,
                    BranchName = pushData.Ref,
                    PushTime = pushEvent.CreatedAt!.Value.UtcDateTime,
                    PusherUserId = pusherId,
                    FilePathsMatched = matchedPaths,
                    Commits = commits.Select(commit => new PushCommitOutput
                    {
                        Hash = commit.Id,
                        Message = commit.Message ?? string.Empty,
                        AuthorName = commit.AuthorName ?? string.Empty,
                        AuthorEmail = commit.AuthorEmail ?? string.Empty
                    }).ToList()
                }));
            });

        EnsureWithinScanLimit(scanStopwatch);
        var repositoryNames = new ConcurrentDictionary<int, string>();
        await Parallel.ForEachAsync(
            matchingPushes.Select(item => item.Push.RepositoryId).Distinct(),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = EnrichmentConcurrency,
                CancellationToken = scanDeadline.Token
            },
            async (repositoryId, cancellationToken) =>
            {
                EnsureWithinScanLimit(scanStopwatch);
                var project = await _client.GetProject(repositoryId, cancellationToken);
                if (string.IsNullOrWhiteSpace(project.PathWithNamespace))
                    throw new PluginApplicationException(
                        $"GitLab project {repositoryId} response omitted path_with_namespace.");
                repositoryNames[repositoryId] = project.PathWithNamespace;
            });

        var pushes = matchingPushes
            .OrderBy(item => item.Index)
            .Select(item => item.Push)
            .ToList();
        foreach (var push in pushes)
            push.RepositoryNameWithNamespace = repositoryNames[push.RepositoryId];

        EnsureWithinScanLimit(scanStopwatch);
        var scanCompletedUtc = _utcNow().ToUniversalTime();
        var retentionLowerUtc = scanCompletedUtc - Overlap;
        var recentProcessedKeys = previousKeys
            .Concat(selectedEvents.Select(item => item.Key))
            .Where(key => key.CreatedAt >= retentionLowerUtc)
            .Distinct()
            .OrderBy(key => key.CreatedAt)
            .ThenBy(key => key.ProjectId)
            .ThenBy(key => key.EventId)
            .ToList();

        return new PollingEventResponse<PushPollingMemory, FilesModifiedPollingResponse>
        {
            FlyBird = pushes.Count > 0,
            Result = pushes.Count > 0 ? new FilesModifiedPollingResponse { Pushes = pushes } : null!,
            Memory = new PushPollingMemory
            {
                LastCompletedScanUtc = scanCompletedUtc,
                InputConfigurationFingerprint = fingerprint,
                RecentProcessedKeys = recentProcessedKeys
            }
        };
    }

    private static Regex[] BuildRegexes(IEnumerable<string> patterns, string label)
    {
        try
        {
            return patterns
                .Select(pattern => new Regex(pattern, RegexOptions.CultureInvariant, RegexTimeout))
                .ToArray();
        }
        catch (ArgumentException exception)
        {
            throw new PluginMisconfigurationException($"Invalid {label} regular expression: {exception.Message}");
        }
    }

    private static bool MatchesAny(IEnumerable<Regex> patterns, string value)
    {
        try
        {
            return patterns.Any(pattern => pattern.IsMatch(value));
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw new PluginMisconfigurationException(
                $"Regular expression evaluation timed out: {exception.Message}");
        }
    }

    private static void EnsureWithinScanLimit(Stopwatch stopwatch)
    {
        if (stopwatch.Elapsed > MaximumScanDuration)
            throw new PluginApplicationException(
                $"GitLab polling scan exceeded the safety time limit of {MaximumScanDuration.TotalMinutes:g} minutes.");
    }
}
