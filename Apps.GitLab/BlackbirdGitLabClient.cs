using Blackbird.Applications.Sdk.Common.Authentication;
using GitLabApiClient;

namespace Apps.Gitlab;

public class BlackbirdGitlabClient
{
    public readonly GitLabClient _client;
    public GitLabClient Client { get => _client; }

    public BlackbirdGitlabClient(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders)
    {
        var apiToken = authenticationCredentialsProviders.First(p => p.KeyName == "Authorization").Value;
        _client = new GitLabClient("https://gitlab.com", apiToken);
    }
}