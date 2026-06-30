using Apps.Gitlab.Actions.Base;
using Apps.Gitlab.Dtos;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Branch.Responses;
using Apps.Gitlab.Models.Respository.Requests;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Invocation;
using GitLabApiClient.Models.Branches.Responses;
using RestSharp;

namespace Apps.Gitlab.Actions;

[ActionList("Branch")]
public class BranchActions(InvocationContext invocationContext)
    : GitLabActions(invocationContext)
{

    [Action("Search branches", Description = "Search repository branches")]
    public async Task<ListRepositoryBranchesResponse> ListRepositoryBranches([ActionParameter] GetRepositoryRequest input)
    {
        var projectId = ParseProjectId(input.RepositoryId);
        var request = RestClient.CreateRequest($"/projects/{projectId}/repository/branches", Method.Get);
        var branches = await RestClient.ExecuteWithErrorHandling<List<Branch>>(request);

        return new ListRepositoryBranchesResponse
        {
            Branches = branches.Select(b => new BranchDto(b))
        };
    }

    [Action("Get branch", Description = "Get branch details by name")]
    public async Task<BranchDto> GetBranch(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetBranchRequest input)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var request = RestClient.CreateRequest(
            $"/projects/{projectId}/repository/branches/{Uri.EscapeDataString(input.Name)}",
            Method.Get);
        var branch = await RestClient.ExecuteWithErrorHandling<Branch>(request);

        return new BranchDto(branch);
    }

    [Action("Create branch", Description = "Create branch from a base branch")]
    public async Task<BranchDto> CreateBranch(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] Models.Branch.Requests.CreateBranchRequest input)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var request = RestClient.CreateRequest($"/projects/{projectId}/repository/branches", Method.Post);
        request.AddParameter("branch", input.NewBranchName);
        request.AddParameter("ref", input.BaseBranchName);

        var branch = await RestClient.ExecuteWithErrorHandling<Branch>(request);
        return new BranchDto(branch);
    }
}
