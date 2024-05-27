using Apps.GitLab.Dtos;
using Blackbird.Applications.Sdk.Common.Authentication;
using GitLabApiClient;
using GitLabApiClient.Internal.Paths;
using GitLabApiClient.Models.Commits.Responses;
using RestSharp;

namespace Apps.Gitlab;

public class BlackbirdGitlabClient
{
    public readonly GitLabClient _client;
    public GitLabClient Client { get => _client; }

    private const string ApiUrl = "https://gitlab.com";

    private IEnumerable<AuthenticationCredentialsProvider> AuthenticationCredentials { get; set; }

    public BlackbirdGitlabClient(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders)
    {
        AuthenticationCredentials = authenticationCredentialsProviders;
        var apiToken = authenticationCredentialsProviders.First(p => p.KeyName == "Authorization").Value;
        _client = new GitLabClient(ApiUrl, apiToken);
    }

    public async Task<byte[]> GetArchive(ProjectId projectId, string? branchName)
    {
        var branchCommit = !string.IsNullOrWhiteSpace(branchName) ? $"?sha={branchName}" : "";
        var request = new RestRequest($"/api/v4/projects/{projectId}/repository/archive.zip{branchCommit}", Method.Get);
        request.AddHeader("Authorization", $"Bearer {AuthenticationCredentials.First(p => p.KeyName == "Authorization").Value}");
        var result = await new RestClient(ApiUrl).ExecuteAsync(request);
        return result.RawBytes;
    }

    public async Task<Commit> PushChanges(ProjectId projectId, string? branchName, string commitMessage, string filePath, byte[] file, string action)
    {
        var repository = await Client.Projects.GetAsync(projectId);

        var request = new RestRequest($"/api/v4/projects/{projectId}/repository/commits", Method.Post);
        request.AddHeader("Authorization", $"Bearer {AuthenticationCredentials.First(p => p.KeyName == "Authorization").Value}");
        request.AddJsonBody(new
        {
            branch = branchName ?? repository.DefaultBranch,
            commit_message = commitMessage,
            actions = new[]
            {
                new FileActionDto(action, filePath, file)
            }
        });
        var result = await new RestClient(ApiUrl).ExecuteAsync<Commit>(request);
        if (!result.IsSuccessStatusCode)
            throw new GitLabFriendlyException(result.Content);
        return result.Data;
    }
}