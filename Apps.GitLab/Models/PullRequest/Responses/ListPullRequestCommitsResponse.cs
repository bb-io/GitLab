using Apps.Gitlab.Dtos;

namespace Apps.Gitlab.Models.PullRequest.Responses;

public class ListPullRequestCommitsResponse
{
    public IEnumerable<PullRequestCommitDto> Commits { get; set; }
}