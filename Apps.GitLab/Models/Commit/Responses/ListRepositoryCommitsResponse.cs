
using GitLabApiClient.Models.Commits.Responses;

public class ListRepositoryCommitsResponse
{
    public IEnumerable<Commit> Commits { get; set; }
}