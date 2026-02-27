using Apps.Gitlab.Actions.Base;
using Apps.Gitlab.Models.PullRequest.Requests;
using Apps.Gitlab.Models.PullRequest.Responses;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.GitLab.Utils;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using GitLabApiClient.Internal.Paths;
using GitLabApiClient.Models.MergeRequests.Requests;
using GitLabApiClient.Models.MergeRequests.Responses;

namespace Apps.Gitlab.Actions;

[ActionList("Pull request")]
public class PullRequestActions : GitLabActions
{
    private readonly IFileManagementClient _fileManagementClient;

    public PullRequestActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
        : base(invocationContext)
    {
        _fileManagementClient = fileManagementClient;
    }

    [Action("List merge requests", Description = "List merge requests")]
    public async Task<ListPullRequestsResponse> ListPullRequests(
        [ActionParameter] GetRepositoryRequest repositoryRequest)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        var pulls = await ErrorHandler.ExecuteWithErrorHandlingAsync(() =>
        Client.MergeRequests.GetAsync(projectId, _ => { }));
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
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        var pull = await ErrorHandler.ExecuteWithErrorHandlingAsync(() =>
        Client.MergeRequests.GetAsync(projectId, int.Parse(input.PullRequestId)));
        return pull;
    }

    [Action("Create merge request", Description = "Create merge request")]
    public async Task<MergeRequest> CreatePullRequest(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] CreatePullRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        var request = new CreateMergeRequest(input.BaseBranch, input.HeadBranch, input.Title)
        {
            Description = input.Description
        };

        var pull = await ErrorHandler.ExecuteWithErrorHandlingAsync(() =>
            Client.MergeRequests.CreateAsync(projectId, request));

        return pull;
    }

    [Action("Complete merge request", Description = "Complete merge request")]
    public async Task<MergeRequest> MergePullRequest(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetPullRequest mergeRequest,
        [ActionParameter] MergePullRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        var acceptRequest = new AcceptMergeRequest
        {
            MergeCommitMessage = input.MergeCommitMessage
        };

        return await ErrorHandler.ExecuteWithErrorHandlingAsync(() =>
            Client.MergeRequests.AcceptAsync(projectId, int.Parse(mergeRequest.PullRequestId), acceptRequest));
    }
}