using GitLabApiClient.Models.Branches.Responses;

namespace Apps.Gitlab.Dtos;

public class BranchDto
{
    public BranchDto(Branch source) 
    {
        Name = source.Name;
        Protected = source.Protected;
        Default = source.Default;
    }

    public string Name { get; set; }

    public bool Protected { get; set; }

    public bool Default { get; set; }
}