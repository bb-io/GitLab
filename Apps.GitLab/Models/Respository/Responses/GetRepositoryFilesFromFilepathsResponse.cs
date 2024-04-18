using Apps.Gitlab.Models.Commit.Responses;

namespace Apps.Gitlab.Models.Respository.Responses;

public class GetRepositoryFilesFromFilepathsResponse
{
    public IEnumerable<GithubFile> Files { get; set; }
}