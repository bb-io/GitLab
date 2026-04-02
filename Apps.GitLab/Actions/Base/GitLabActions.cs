using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.Gitlab.Actions.Base;

public class GitLabActions : BaseInvocable
{
    protected IEnumerable<AuthenticationCredentialsProvider> Creds =>
        InvocationContext.AuthenticationCredentialsProviders;

    protected BlackbirdGitlabClient RestClient { get; }

    public GitLabActions(InvocationContext invocationContext) : base(invocationContext)
    {
        RestClient = new(Creds);
    }

    protected static int ParseProjectId(string repositoryId)
        => int.Parse(repositoryId);
}
