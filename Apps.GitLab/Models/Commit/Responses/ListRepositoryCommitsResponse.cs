
using GitLabApiClient.Models.Commits.Responses;

public class ListRepositoryCommitsResponse
{
    public int Count { get; set; }

    public IEnumerable<Commit> Commits { get; set; } = [];
}
