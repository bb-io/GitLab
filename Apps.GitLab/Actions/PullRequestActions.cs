using Apps.Gitlab.Actions.Base;
using Apps.Gitlab.Dtos;
using Apps.Gitlab.Models.PullRequest.Requests;
using Apps.Gitlab.Models.PullRequest.Responses;
using Apps.Gitlab.Models.Respository.Requests;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using GitLabApiClient.Internal.Paths;
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
        var pulls = await Client.MergeRequests.GetAsync(projectId, (options) => { });
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
        var pull = await Client.MergeRequests.GetAsync(projectId, int.Parse(input.PullRequestId));
        return pull;
    }

    [Action("Create merge request", Description = "Create merge request")]
    public async Task<MergeRequest> CreatePullRequest(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] CreatePullRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        var pull = await Client.MergeRequests.CreateAsync(projectId, new GitLabApiClient.Models.MergeRequests.Requests.CreateMergeRequest(input.BaseBranch, input.HeadBranch, input.Title) { Description = input.Description});
        return pull;
    }

    //[Action("Merge pull request", Description = "Merge pull request")]
    //public PullRequestMerge MergePullRequest(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
    //    [ActionParameter] GetRepositoryRequest repositoryRequest,
    //    [ActionParameter] Models.PullRequest.Requests.MergePullRequest input)
    //{
    //    var client = new BlackbirdGitlabClient(authenticationCredentialsProviders);
    //    return client.PullRequest.Merge(long.Parse(repositoryRequest.RepositoryId), int.Parse(input.PullRequestNumber), new Octokit.MergePullRequest()).Result;
    //}

    //[Action("List pull request files", Description = "List pull request files")]
    //public ListPullRequestFilesResponse ListPullRequestFiles(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
    //    [ActionParameter] GetRepositoryRequest repositoryRequest,
    //    [ActionParameter] ListPullFilesRequest input)
    //{
    //    var client = new BlackbirdGitlabClient(authenticationCredentialsProviders);
    //    var files = client.PullRequest.Files(long.Parse(repositoryRequest.RepositoryId), int.Parse(input.PullRequestNumber)).Result;
    //    return new ListPullRequestFilesResponse
    //    {
    //        Files = files
    //    };
    //}

    //[Action("List pull request commits", Description = "List pull request commits")]
    //public ListPullRequestCommitsResponse ListPullRequestCommits(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
    //    [ActionParameter] GetRepositoryRequest repositoryRequest,
    //    [ActionParameter] GetPullRequest input)
    //{
    //    var client = new BlackbirdGitlabClient(authenticationCredentialsProviders);
    //    var commits = client.PullRequest.Commits(long.Parse(repositoryRequest.RepositoryId), int.Parse(input.PullRequestNumber)).Result;
    //    return new ListPullRequestCommitsResponse
    //    {
    //        Commits = commits.Select(p => new PullRequestCommitDto(p))
    //    };
    //}

    //[Action("Is pull request merged", Description = "Is pull request merged")]
    //public IsPullMergedResponse IsPullMerged(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
    //    [ActionParameter] GetRepositoryRequest repositoryRequest,
    //    [ActionParameter] GetPullRequest input)
    //{
    //    var client = new BlackbirdGitlabClient(authenticationCredentialsProviders);
    //    return new IsPullMergedResponse
    //    {
    //        IsPullMerged = client.PullRequest.Merged(long.Parse(repositoryRequest.RepositoryId), int.Parse(input.PullRequestNumber)).Result,
    //    };
    //}
}