using Apps.Gitlab.Dtos;

namespace Apps.Gitlab.Models.Commit.Responses;

public class ListRepositoryCommitsResponse
{
    public IEnumerable<SmallCommitDto> Commits { get; set; }
}