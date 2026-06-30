using Blackbird.Applications.Sdk.Common;

namespace Apps.GitLab.Models.Commit.Responses;

public class ListRepositoryCommitsResponse
{
    [Display("Count")]
    public int Count { get; set; }

    [Display("Commits")]
    public IEnumerable<CommitResponse> Commits { get; set; } = [];
}
