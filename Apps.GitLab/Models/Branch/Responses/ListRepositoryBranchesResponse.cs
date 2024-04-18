using Apps.Gitlab.Dtos;

namespace Apps.Gitlab.Models.Branch.Responses;

public class ListRepositoryBranchesResponse
{
    public IEnumerable<BranchDto> Branches { get; set; }
}