using Apps.Gitlab.Models.Commit.Responses;

using Blackbird.Applications.Sdk.Common;

using Apps.GitLab.Models.Responses;

namespace Apps.GitLab.Models.Respository.Responses;

public class GetRepositoryFilesFromFilepathsResponse
{
    public IEnumerable<GitLabFile> Files { get; set; }

    [Display("Number of units")]
    public int NumberOfUnits { get; set; }

    [Display("Metadata")]
    public IEnumerable<MetadataResponse> Metadata { get; set; } = [];
}
