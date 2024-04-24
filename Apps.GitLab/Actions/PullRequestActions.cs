using Apps.Gitlab.Actions.Base;
using Apps.Gitlab.Dtos;
using Apps.Gitlab.Models.PullRequest.Requests;
using Apps.Gitlab.Models.PullRequest.Responses;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.GitLab.Dtos;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using GitLabApiClient;
using GitLabApiClient.Internal.Paths;
using GitLabApiClient.Models.MergeRequests.Requests;
using GitLabApiClient.Models.MergeRequests.Responses;
using System.Collections.Generic;

namespace Apps.Gitlab.Actions;

[ActionList]
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
        try {
            var pulls = await Client.MergeRequests.GetAsync(projectId, (options) => { });
            return new ListPullRequestsResponse
            {
                PullRequests = pulls
            };
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("Get merge request", Description = "Get merge request")]
    public async Task<MergeRequest> GetPullRequest(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetPullRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        try {
            var pull = await Client.MergeRequests.GetAsync(projectId, int.Parse(input.PullRequestId));
            return pull;
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("Create merge request", Description = "Create merge request")]
    public async Task<MergeRequest> CreatePullRequest(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] CreatePullRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        try {
            var pull = await Client.MergeRequests.CreateAsync(projectId, new GitLabApiClient.Models.MergeRequests.Requests.CreateMergeRequest(input.BaseBranch, input.HeadBranch, input.Title) { Description = input.Description});
            return pull;
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("Complete merge request", Description = "Complete merge request")]
    public async Task<MergeRequest> MergePullRequest(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetPullRequest mergeRequest,
        [ActionParameter] MergePullRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        try {
            return await Client.MergeRequests.AcceptAsync(projectId, int.Parse(mergeRequest.PullRequestId), new AcceptMergeRequest() { MergeCommitMessage = input.MergeCommitMessage });
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }
}