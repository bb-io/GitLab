using GitLabApiClient.Models.Trees.Responses;

namespace Apps.Gitlab.Models.Respository.Responses;

public class RepositoryContentResponse
{
    public IEnumerable<Tree> Content { get; set; }
}