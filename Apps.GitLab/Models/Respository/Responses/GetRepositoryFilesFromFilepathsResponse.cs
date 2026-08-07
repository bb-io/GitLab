using Apps.Gitlab.Models.Commit.Responses;

namespace Apps.Gitlab.Models.Respository.Responses;

public class GetRepositoryFilesFromFilepathsResponse
{
    public IEnumerable<GitLabFile> Files { get; set; }

    public IEnumerable<Apps.GitLab.Models.Responses.MetadataResponse> Metadata { get; set; } = [];
}
