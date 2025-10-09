using Apps.Gitlab.Dtos;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Branch.Responses;
using Apps.Gitlab.Models.Respository.Requests;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using GitLabApiClient.Internal.Paths;
using Apps.Gitlab.Actions.Base;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using GitLabApiClient;
using Apps.GitLab.Dtos;

namespace Apps.Gitlab.Actions;

[ActionList("Branch")]
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
        try {
            var branches = await Client.Branches.GetAsync(projectId, (options) => { });
            return new ListRepositoryBranchesResponse
            {
                Branches = branches.Select(b => new BranchDto(b))
            };
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("Get branch", Description = "Get branch by name")]
    public async Task<BranchDto> GetBranch(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetBranchRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        try 
        {
            var branch = await Client.Branches.GetAsync(projectId, input.Name);
            return new BranchDto(branch);
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("Create branch", Description = "Create branch")]
    public async Task<BranchDto> CreateBranch(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] Models.Branch.Requests.CreateBranchRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        try
        {
            var branch = await Client.Branches.CreateAsync(projectId, new GitLabApiClient.Models.Branches.Requests.CreateBranchRequest(input.NewBranchName, input.BaseBranchName));
            return new BranchDto(branch);
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }
}