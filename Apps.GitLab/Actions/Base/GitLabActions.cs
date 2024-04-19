using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using GitLabApiClient;

namespace Apps.Gitlab.Actions.Base;

public class GitLabActions : BaseInvocable
{
    protected IEnumerable<AuthenticationCredentialsProvider> Creds =>
        InvocationContext.AuthenticationCredentialsProviders;
    
    private BlackbirdGitlabClient GitLabClient { get; set; }

    protected GitLabClient Client { get => GitLabClient.Client; }
    protected BlackbirdGitlabClient RestClient { get => GitLabClient; }

    public GitLabActions(InvocationContext invocationContext) : base(invocationContext)
    {
        GitLabClient = new(Creds);
    }
}