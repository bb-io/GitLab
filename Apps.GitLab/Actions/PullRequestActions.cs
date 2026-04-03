using Apps.Gitlab.Actions.Base;
using Apps.Gitlab.Models.PullRequest.Requests;
using Apps.Gitlab.Models.PullRequest.Responses;
using Apps.Gitlab.Models.Respository.Requests;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Apps.GitLab.Utils;
using GitLabApiClient.Models.MergeRequests.Responses;
using RestSharp;

namespace Apps.Gitlab.Actions;

[ActionList("Pull request")]
public class PullRequestActions : GitLabActions
{
    public PullRequestActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
        : base(invocationContext)
    {
    }

    [Action("List merge requests", Description = "List merge requests")]
    public async Task<ListPullRequestsResponse> ListPullRequests(
        [ActionParameter] GetRepositoryRequest repositoryRequest)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var request = RestClient.CreateRequest($"/projects/{projectId}/merge_requests", Method.Get);
        var pulls = await RestClient.ExecuteWithErrorHandling<List<MergeRequest>>(request);

        return new ListPullRequestsResponse
        {
            PullRequests = pulls
        };
    }

    [Action("Get merge request", Description = "Get merge request")]
    public async Task<MergeRequest> GetPullRequest(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetPullRequest input)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var mergeRequestId = ParsingUtils.ParseIntOrThrow(input.PullRequestId, "Pull request ID");
        var request = RestClient.CreateRequest(
            $"/projects/{projectId}/merge_requests/{mergeRequestId}",
            Method.Get);

        return await RestClient.ExecuteWithErrorHandling<MergeRequest>(request);
    }

    [Action("Create merge request", Description = "Create merge request")]
    public async Task<MergeRequest> CreatePullRequest(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] CreatePullRequest input)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var request = RestClient.CreateRequest($"/projects/{projectId}/merge_requests", Method.Post);
        request.AddJsonBody(new
        {
            source_branch = input.HeadBranch,
            target_branch = input.BaseBranch,
            title = input.Title,
            description = input.Description
        });

        return await RestClient.ExecuteWithErrorHandling<MergeRequest>(request);
    }

    [Action("Complete merge request", Description = "Complete merge request")]
    public async Task<MergeRequest> MergePullRequest(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetPullRequest mergeRequest,
        [ActionParameter] MergePullRequest input)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var mergeRequestId = ParsingUtils.ParseIntOrThrow(mergeRequest.PullRequestId, "Pull request ID");
        var request = RestClient.CreateRequest(
            $"/projects/{projectId}/merge_requests/{mergeRequestId}/merge",
            Method.Put);
        request.AddJsonBody(new
        {
            merge_commit_message = input.MergeCommitMessage
        });

        return await RestClient.ExecuteWithErrorHandling<MergeRequest>(request);
    }
}
