using Apps.Gitlab.Models.Commit.Responses;

using Blackbird.Applications.Sdk.Common;

namespace Apps.Gitlab.Models.Respository.Responses;

public class GetRepositoryFilesFromFilepathsResponse
{
    public IEnumerable<GitLabFile> Files { get; set; }

    [Display("Number of units")]
    public int NumberOfUnits { get; set; }

    public IEnumerable<Apps.GitLab.Models.Responses.MetadataResponse> Metadata { get; set; } = [];
}
