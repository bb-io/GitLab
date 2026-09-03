using System.Net;
using System.Text.RegularExpressions;
using Apps.Gitlab.Webhooks.Payloads;
using Apps.GitLab.Webhooks.Handlers;
using Apps.GitLab.Webhooks.Payloads;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Webhooks;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Apps.GitLab.Webhooks;

[WebhookList("Pushes")]
public class GroupPushWebhooks(InvocationContext invocationContext) : BaseInvocable(invocationContext)
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    [Webhook("On files modified in groups", typeof(GroupPushEventHandler),
        Description = "Triggered when modified files in repositories under selected groups match configured filters")]
    public Task<WebhookResponse<CrossRepositoryFileModifiedResponse>> FilesModifiedInGroups(
        WebhookRequest webhookRequest,
        [WebhookParameter(true)] GroupWebhookInput groupInput,
        [WebhookParameter] CrossRepositoryFileModifiedInput input)
    {
        var payload = JsonConvert.DeserializeObject<PushPayload>(webhookRequest.Body?.ToString() ?? string.Empty);
        if (payload is null)
            throw new InvalidCastException(nameof(webhookRequest.Body));

        if (!string.Equals(payload.ObjectKind, "push", StringComparison.Ordinal) ||
            !string.Equals(payload.EventName, "push", StringComparison.Ordinal) ||
            payload.Ref is null || !payload.Ref.StartsWith("refs/heads/", StringComparison.Ordinal))
            return Task.FromResult(Preflight());

        var branchName = payload.Ref["refs/heads/".Length..];
        var repositoryId = payload.ProjectId.ToString();
        var userId = payload.UserId.ToString();

        var watchedRepositories = input.RepositoryIds?.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet() ?? [];
        var ignoredRepositories = input.IgnoredRepositoryIds?.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet() ?? [];
        if (ignoredRepositories.Contains(repositoryId) ||
            (watchedRepositories.Count > 0 && !watchedRepositories.Contains(repositoryId)))
            return Task.FromResult(Preflight());

        var watchedUsers = input.UserIds?.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet() ?? [];
        var ignoredUsers = input.IgnoredUserIds?.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet() ?? [];
        if (ignoredUsers.Contains(userId) || (watchedUsers.Count > 0 && !watchedUsers.Contains(userId)))
            return Task.FromResult(Preflight());

        try
        {
            if (MatchesAny(input.IgnoredBranchPatterns, branchName) ||
                (input.BranchPatterns?.Any(x => !string.IsNullOrWhiteSpace(x)) == true &&
                 !MatchesAny(input.BranchPatterns, branchName)))
                return Task.FromResult(Preflight());

            var matchers = new List<Matcher>();
            foreach (var filePattern in input.FilePatterns?.Where(x => !string.IsNullOrWhiteSpace(x)) ?? [])
            {
                var normalizedPattern = filePattern.Contains('/') ? filePattern : $"**/{filePattern}";
                var matcher = new Matcher();
                matcher.AddInclude(normalizedPattern);
                matchers.Add(matcher);
            }

            var receivedCommits = (payload.Commits ?? []).Take(20).ToList();
            var matchedPaths = new List<string>();
            var seenPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (var commit in receivedCommits)
            {
                var message = commit.Message ?? string.Empty;
                if (MatchesAny(input.ExcludedCommitMessagePatterns, message) ||
                    (input.IncludedCommitMessagePatterns?.Any(x => !string.IsNullOrWhiteSpace(x)) == true &&
                     !MatchesAny(input.IncludedCommitMessagePatterns, message)))
                    continue;

                foreach (var filePath in commit.Modified ?? [])
                {
                    if ((matchers.Count == 0 || matchers.Any(x => x.Match(filePath).HasMatches)) &&
                        seenPaths.Add(filePath))
                        matchedPaths.Add(filePath);
                }
            }

            if (matchedPaths.Count == 0)
                return Task.FromResult(Preflight());

            var commits = receivedCommits.Select(x => new PushCommitOutput
            {
                Hash = x.Id ?? string.Empty,
                Message = x.Message ?? string.Empty,
                AuthorName = x.Author?.Name ?? string.Empty,
                AuthorEmail = x.Author?.Email ?? string.Empty
            }).ToList();

            return Task.FromResult(new WebhookResponse<CrossRepositoryFileModifiedResponse>
            {
                HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK),
                Result = new CrossRepositoryFileModifiedResponse
                {
                    RepositoryId = payload.ProjectId,
                    RepositoryNameWithNamespace = payload.Project?.PathWithNamespace ?? string.Empty,
                    BranchName = branchName,
                    Commits = commits,
                    FilePathsMatched = matchedPaths,
                    PushUserId = payload.UserId,
                    TotalCommitsCount = payload.TotalCommitsCount,
                    CommitsTruncated = payload.TotalCommitsCount > commits.Count
                }
            });
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            InvocationContext.Logger?.LogError(
                $"Invalid or timing-out regular expression or glob in group push webhook filters: {ex.Message}", []);
            return Task.FromResult(Preflight());
        }
    }

    private static bool MatchesAny(IEnumerable<string>? patterns, string value)
    {
        return patterns?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Any(x => Regex.IsMatch(value, x, RegexOptions.CultureInvariant, RegexTimeout)) == true;
    }

    private static WebhookResponse<CrossRepositoryFileModifiedResponse> Preflight()
    {
        return new WebhookResponse<CrossRepositoryFileModifiedResponse>
        {
            HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK),
            Result = null,
            ReceivedWebhookRequestType = WebhookRequestType.Preflight
        };
    }
}
