using System.Globalization;
using System.Text.RegularExpressions;
using Apps.Gitlab;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.GitLab.Diagnostics;
using Apps.GitLab.Models.Diagnostics;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.GitLab.Actions;

[ActionList("Diagnostics")]
public class PushDiagnosticActions(InvocationContext invocationContext) : BaseInvocable(invocationContext)
{
    [Action("Diagnose event stream",
        Description = "Compare global and repository push feeds around a specified timestamp")]
    public Task<DiagnoseEventStreamResponse> DiagnoseEventStream(
        [ActionParameter] GetRepositoryRequest repository,
        [ActionParameter] DiagnoseEventStreamRequest input)
    {
        if (!int.TryParse(repository.RepositoryId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
            throw new PluginMisconfigurationException("Enter a positive numeric Repository ID.");
        if (!Regex.IsMatch(input.Timestamp ?? string.Empty,
                @"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})\z") ||
            !DateTimeOffset.TryParse(input.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
            throw new PluginMisconfigurationException(
                "Enter Timestamp in ISO 8601 format with Z or a timezone offset, for example 2026-11-04T02:57:34.718-03:30.");
        if (input.MinutesBeforeAndAfter is < 1 or > 1440)
            throw new PluginMisconfigurationException("Enter Minutes before and after between 1 and 1440.");
        if (timestamp.UtcDateTime.Year is < 2 or > 9998)
            throw new PluginMisconfigurationException("Enter a Timestamp between years 2 and 9998.");

        return new FilesModifiedDiagnostics(new BlackbirdGitlabClient(
                InvocationContext.AuthenticationCredentialsProviders))
            .CollectEventStreamAsync(id, timestamp, input.MinutesBeforeAndAfter);
    }

    [Action("Diagnose files modified event",
        Description = "Collect read-only commit and push evidence to investigate a missing event Flight")]
    public Task<DiagnoseFilesModifiedResponse> DiagnoseFilesModifiedEvent(
        [ActionParameter] GetRepositoryRequest repository,
        [ActionParameter] DiagnoseFilesModifiedRequest input)
    {
        if (!int.TryParse(repository.RepositoryId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
            throw new PluginMisconfigurationException("Enter a positive numeric Repository ID.");
        var sha = input.CommitSha ?? string.Empty;
        if (!Regex.IsMatch(sha, @"\A[0-9a-fA-F]{40}\z"))
            throw new PluginMisconfigurationException("Enter the full 40-character Commit SHA.");
        if (string.IsNullOrWhiteSpace(input.ExpectedBranch))
            throw new PluginMisconfigurationException("Enter the Expected branch.");

        return new FilesModifiedDiagnostics(new BlackbirdGitlabClient(
                InvocationContext.AuthenticationCredentialsProviders))
            .CollectAsync(id, sha.ToLowerInvariant(), input.ExpectedBranch);
    }
}
