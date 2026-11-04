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
