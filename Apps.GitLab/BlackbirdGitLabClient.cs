using Apps.GitLab.Constants;
using Apps.GitLab.Dtos;
using Apps.Gitlab.Constants;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using Blackbird.Applications.Sdk.Utils.Extensions.String;
using Blackbird.Applications.Sdk.Utils.RestSharp;
using GitLabApiClient.Models.Commits.Responses;
using Newtonsoft.Json;
using RestSharp;

namespace Apps.Gitlab;

public class BlackbirdGitlabClient : BlackBirdRestClient
{
    private readonly IEnumerable<AuthenticationCredentialsProvider> _authenticationCredentials;

    protected override JsonSerializerSettings? JsonSettings => JsonConfig.JsonSettings;

    public string BaseUrl { get; }

    public BlackbirdGitlabClient(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders)
        : base(new()
        {
            BaseUrl = GetBaseUrl(authenticationCredentialsProviders).ToUri()
        })
    {
        _authenticationCredentials = authenticationCredentialsProviders;
        BaseUrl = GetBaseUrl(authenticationCredentialsProviders);
    }

    public static string GetBaseUrl(IEnumerable<AuthenticationCredentialsProvider> creds)
    {
        var connectionType = creds.Get(CredNames.ConnectionType).Value;

        return connectionType switch
        {
            ConnectionTypes.OAuth => "https://gitlab.com",
            ConnectionTypes.OAuthSelfManaged => creds.Get(CredNames.BaseUrl).Value.TrimEnd('/'),
            ConnectionTypes.PersonalAccessToken => "https://gitlab.com",
            _ => throw new Exception($"Unsupported connection type: {connectionType}")
        };
    }

    public GitLabRequest CreateRequest(string resource, Method method)
        => new(resource, method, _authenticationCredentials);

    public async Task<byte[]> ExecuteForBytesWithErrorHandling(RestRequest request)
    {
        var response = await ExecuteWithErrorHandling(request);

        return response.RawBytes ?? [];
    }

    public async Task<byte[]> GetArchive(int projectId, string? branchName)
    {
        var branchCommit = !string.IsNullOrWhiteSpace(branchName) ? $"?sha={Uri.EscapeDataString(branchName)}" : "";
        var request = CreateRequest($"/api/v4/projects/{projectId}/repository/archive.zip{branchCommit}", Method.Get);

        return await ExecuteForBytesWithErrorHandling(request);
    }

    public async Task<Commit> PushChanges(int projectId, string? branchName, string commitMessage,
        string filePath, byte[]? file, string action)
    {
        var repository = await ExecuteWithErrorHandling<RepositoryInfo>(
            CreateRequest($"/api/v4/projects/{projectId}", Method.Get));

        var request = CreateRequest($"/api/v4/projects/{projectId}/repository/commits", Method.Post);
        request.AddJsonBody(new
        {
            branch = branchName ?? repository.DefaultBranch,
            commit_message = commitMessage,
            actions = new[]
            {
                new FileActionDto(action, filePath, file)
            }
        });

        return await ExecuteWithErrorHandling<Commit>(request);
    }

    protected override Exception ConfigureErrorException(RestResponse response)
    {
        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                new PluginMisconfigurationException(
                    "GitLab credentials are invalid or do not have sufficient permissions. Please check your access token and scopes."),
            System.Net.HttpStatusCode.NotFound =>
                new PluginApplicationException("Resource was not found in GitLab. Please verify the repository ID / path / branch."),
            (System.Net.HttpStatusCode)429 =>
                new PluginApplicationException("GitLab rate limit reached. Please try again later."),
            _ => new PluginApplicationException(response.Content ?? "GitLab request failed.")
        };
    }

    private class RepositoryInfo
    {
        [JsonProperty("default_branch")]
        public string? DefaultBranch { get; set; }
    }
}
