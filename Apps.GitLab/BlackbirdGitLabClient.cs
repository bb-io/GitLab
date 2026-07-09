using System.Net;
using Apps.GitLab.Constants;
using Apps.GitLab.Dtos;
using Apps.Gitlab.Constants;
using Apps.GitLab.Models.Respository.Responses;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using Blackbird.Applications.Sdk.Utils.Extensions.String;
using Blackbird.Applications.Sdk.Utils.RestSharp;
using GitLabApiClient.Models.Projects.Responses;
using Newtonsoft.Json;
using RestSharp;
using Commit = GitLabApiClient.Models.Commits.Responses.Commit;

namespace Apps.Gitlab;

public class BlackbirdGitlabClient : BlackBirdRestClient
{
    private const string ApiPrefix = "/api/v4";
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
    
    public async Task<RestResponse> ExecuteWithErrorHandling(RestRequest request, params HttpStatusCode[] allowedStatuses)
    {
        var response = await ExecuteAsync(request);
        if (response.IsSuccessStatusCode || allowedStatuses.Contains(response.StatusCode))
            return response;
        
        throw ConfigureErrorException(response);
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
        => new(NormalizeApiResource(resource), method, _authenticationCredentials);

    public async Task<byte[]> ExecuteForBytesWithErrorHandling(RestRequest request)
    {
        var response = await ExecuteWithErrorHandling(request);

        return response.RawBytes ?? [];
    }
    
    public async Task<Project> GetProject(int projectId)
    {
        var request = CreateRequest($"/projects/{projectId}", Method.Get);
        return await ExecuteWithErrorHandling<Project>(request);
    }
    
    public async Task<RepositoryFileResponse> GetFileInfo(int projectId, string filePath, string branch)
    {
        string endpoint = $"/projects/{projectId}/repository/files/{Uri.EscapeDataString(filePath)}";
        var request = CreateRequest(endpoint, Method.Get).AddQueryParameter("ref", branch);

        return await ExecuteWithErrorHandling<RepositoryFileResponse>(request);
    }

    public async Task<byte[]> GetArchive(int projectId, string? branchName)
    {
        var branchCommit = !string.IsNullOrWhiteSpace(branchName) ? $"?sha={Uri.EscapeDataString(branchName)}" : "";
        var request = CreateRequest($"/projects/{projectId}/repository/archive.zip{branchCommit}", Method.Get);

        return await ExecuteForBytesWithErrorHandling(request);
    }

    public async Task<Commit> PushChanges(int projectId, string? branchName, string commitMessage,
        string filePath, byte[]? file, string action)
    {
        var branch = string.IsNullOrWhiteSpace(branchName) ? (await GetProject(projectId)).DefaultBranch : branchName;

        var request = CreateRequest($"/projects/{projectId}/repository/commits", Method.Post);
        request.AddJsonBody(new
        {
            branch,
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
        var message = $"{(int)response.StatusCode}: {response.ErrorMessage ?? response.Content}";

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                new PluginMisconfigurationException(message),
            _ => new PluginApplicationException(message)
        };
    }

    private static string NormalizeApiResource(string resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
            return ApiPrefix;

        if (resource.StartsWith(ApiPrefix, StringComparison.OrdinalIgnoreCase))
            return resource;

        return $"{ApiPrefix}{resource}";
    }
}
