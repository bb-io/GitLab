using Apps.Gitlab.Dtos;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Branch.Responses;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.Gitlab.Webhooks.Payloads;
using Apps.GitHub.Models.Branch.Requests;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Authentication;
using GitLabApiClient.Internal.Paths;
using Apps.Gitlab.Actions.Base;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using GitLabApiClient.Models.Branches.Requests;

namespace Apps.Gitlab.Actions;

[ActionList]
public class BranchActions : GitLabActions
{
    private readonly IFileManagementClient _fileManagementClient;

    public BranchActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
        : base(invocationContext)
    {
        _fileManagementClient = fileManagementClient;
    }

    [Action("List branches", Description = "List respository branches")]
    public async Task<ListRepositoryBranchesResponse> ListRepositoryBranches([ActionParameter] GetRepositoryRequest input)
    {
        var projectId = (ProjectId)int.Parse(input.RepositoryId);
        var branches = await Client.Branches.GetAsync(projectId, GetBranchSearchOptions);
        return new ListRepositoryBranchesResponse
        {
            Branches = branches.Select(b => new BranchDto(b))
        };
    }

    [Action("Get branch", Description = "Get branch by name")]
    public async Task<BranchDto> GetBranch(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetBranchRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        var branch = await Client.Branches.GetAsync(projectId, input.Name);
        return new BranchDto(branch);
    }

    //[Action("Merge branch", Description = "Merge branch")]
    //public MergeDto MergeBranch(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
    //    [ActionParameter] GetRepositoryRequest repositoryRequest,
    //    [ActionParameter] MergeBranchRequest input)
    //{
    //    var client = new BlackbirdGitlabClient(authenticationCredentialsProviders);
    //    var mergeRequest = new NewMerge(input.BaseBranch, input.HeadBranch) { CommitMessage = input.CommitMessage };
    //    var merge = client.Repository.Merging.Create(long.Parse(repositoryRequest.RepositoryId), mergeRequest).Result;
    //    return new MergeDto(merge);
    //}

    //[Action("Create branch", Description = "Create branch")]
    //public async Task<BranchDto> CreateBranch(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
    //    [ActionParameter] GetRepositoryRequest repositoryRequest,
    //    [ActionParameter] CreateBranchRequest input)
    //{
    //    var client = new BlackbirdGitlabClient(authenticationCredentialsProviders);
    //    var master = await client.Git.Reference.Get(long.Parse(repositoryRequest.RepositoryId), $"heads/{input.BaseBranchName}");
    //    await client.Git.Reference.Create(long.Parse(repositoryRequest.RepositoryId), new NewReference("refs/heads/" + input.NewBranchName, master.Object.Sha));
    //    return GetBranch(authenticationCredentialsProviders, repositoryRequest, new GetBranchRequest() { Name = input.NewBranchName });
    //}

    public static void GetBranchSearchOptions(BranchQueryOptions options)
    {
    }
}