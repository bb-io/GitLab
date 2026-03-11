using Apps.GitLab.Constants;
using Apps.GitLab.Dtos;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using Blackbird.Applications.Sdk.Utils.Extensions.String;
using Blackbird.Applications.Sdk.Utils.RestSharp;
using GitLabApiClient;
using GitLabApiClient.Internal.Paths;
using GitLabApiClient.Models.Commits.Responses;
using RestSharp;

namespace Apps.Gitlab;

public class BlackbirdGitlabClient : BlackBirdRestClient
{
    private readonly IEnumerable<AuthenticationCredentialsProvider> _authenticationCredentials;
    private readonly string _baseUrl;

    public readonly GitLabClient _client;
    public GitLabClient Client => _client;

    public BlackbirdGitlabClient(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders)
        : base(new()
        {
            BaseUrl = GetBaseUrl(authenticationCredentialsProviders).ToUri()
        })
    {
        _authenticationCredentials = authenticationCredentialsProviders;
        _baseUrl = GetBaseUrl(authenticationCredentialsProviders);

        var apiToken = authenticationCredentialsProviders.Get(CredNames.Authorization).Value;
        _client = new GitLabClient(_baseUrl, apiToken);
    }

    private static string GetBaseUrl(IEnumerable<AuthenticationCredentialsProvider> creds)
    {
        var connectionType = creds.Get(CredNames.ConnectionType).Value;

        return connectionType switch
        {
            ConnectionTypes.OAuth => "https://gitlab.com",
            ConnectionTypes.OAuthSelfManaged => creds.Get(CredNames.BaseUrl).Value.TrimEnd('/'),
            ConnectionTypes.PersonalAccessToken => creds.Get(CredNames.BaseUrl).Value.TrimEnd('/'),
            _ => throw new Exception($"Unsupported connection type: {connectionType}")
        };
    }

    public async Task<byte[]> GetArchive(ProjectId projectId, string? branchName)
    {
        var branchCommit = !string.IsNullOrWhiteSpace(branchName) ? $"?sha={branchName}" : "";
        var request = new RestRequest($"/api/v4/projects/{projectId}/repository/archive.zip{branchCommit}", Method.Get);
        request.AddHeader("Authorization", $"Bearer {_authenticationCredentials.Get(CredNames.Authorization).Value}");

        var result = await new RestClient(_baseUrl).ExecuteAsync(request);

        if (!result.IsSuccessStatusCode)
            throw ConfigureErrorException(result);

        return result.RawBytes;
    }

    public async Task<Commit> PushChanges(ProjectId projectId, string? branchName, string commitMessage,
        string filePath, byte[] file, string action)
    {
        var repository = await Client.Projects.GetAsync(projectId);

        var request = new RestRequest($"/api/v4/projects/{projectId}/repository/commits", Method.Post);
        request.AddHeader("Authorization", $"Bearer {_authenticationCredentials.Get(CredNames.Authorization).Value}");
        request.AddJsonBody(new
        {
            branch = branchName ?? repository.DefaultBranch,
            commit_message = commitMessage,
            actions = new[]
            {
                new FileActionDto(action, filePath, file)
            }
        });

        var result = await new RestClient(_baseUrl).ExecuteAsync<Commit>(request);

        if (!result.IsSuccessStatusCode)
            throw ConfigureErrorException(result);

        return result.Data;
    }

    protected override Exception ConfigureErrorException(RestResponse response)
    {
        return new PluginApplicationException(response.Content);
    }
}