using Apps.GitLab.Utils;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.Gitlab.DataSourceHandlers;

public abstract class GitLabDataHandler(InvocationContext invocationContext) : BaseInvocable(invocationContext)
{
    protected IEnumerable<AuthenticationCredentialsProvider> Creds =>
        InvocationContext.AuthenticationCredentialsProviders;

    protected BlackbirdGitlabClient RestClient { get; } =
        new(invocationContext.AuthenticationCredentialsProviders);

    protected static int GetProjectId(string? repositoryId)
    {
        if (string.IsNullOrWhiteSpace(repositoryId))
            throw new PluginMisconfigurationException("You should select a repository first.");

        return ParsingUtils.ParseIntOrThrow(repositoryId, "Repository ID");
    }

    protected static string GetDisplayName(string path)
    {
        var normalizedPath = GitLabPathHelper.NormalizePath(path);
        return normalizedPath.Split('/').LastOrDefault() ?? normalizedPath;
    }
}
