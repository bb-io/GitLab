using Blackbird.Applications.Sdk.Common;
using GitLabCommit = GitLabApiClient.Models.Commits.Responses.Commit;
using GitLabCommitStats = GitLabApiClient.Models.Commits.Responses.CommitStats;

namespace Apps.GitLab.Models.Commit.Responses;

public class CommitResponse
{
    [Display("Commit ID")]
    public string Id { get; set; } = string.Empty;

    [Display("Short commit ID")]
    public string ShortId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    [Display("Author name")]
    public string AuthorName { get; set; } = string.Empty;

    [Display("Author email")]
    public string AuthorEmail { get; set; } = string.Empty;

    [Display("Authored date")]
    public DateTime AuthoredDate { get; set; }

    [Display("Committer name")]
    public string CommitterName { get; set; } = string.Empty;

    [Display("Committer email")]
    public string CommitterEmail { get; set; } = string.Empty;

    [Display("Committed date")]
    public DateTime CommittedDate { get; set; }

    [Display("Created at")]
    public DateTime CreatedAt { get; set; }

    public string Message { get; set; } = string.Empty;

    [Display("Parent commit IDs")]
    public List<string> ParentIds { get; set; } = [];

    [Display("Web URL")]
    public string WebUrl { get; set; } = string.Empty;

    [Display("Commit stats")]
    public CommitStatsResponse? CommitStats { get; set; }

    public CommitResponse()
    {
    }

    public CommitResponse(GitLabCommit commit)
    {
        Id = commit.Id;
        ShortId = commit.ShortId;
        Title = commit.Title;
        AuthorName = commit.AuthorName;
        AuthorEmail = commit.AuthorEmail;
        AuthoredDate = commit.AuthoredDate;
        CommitterName = commit.CommitterName;
        CommitterEmail = commit.CommitterEmail;
        CommittedDate = commit.CommittedDate;
        CreatedAt = commit.CreatedAt;
        Message = commit.Message;
        ParentIds = commit.ParentIds ?? [];
        WebUrl = commit.WebUrl;
        CommitStats = commit.CommitStats is null ? null : new(commit.CommitStats);
    }
}

public class CommitStatsResponse
{
    public int Additions { get; set; }

    public int Deletions { get; set; }

    public int Total { get; set; }

    public CommitStatsResponse()
    {
    }

    public CommitStatsResponse(GitLabCommitStats commitStats)
    {
        Additions = commitStats.Additions;
        Deletions = commitStats.Deletions;
        Total = commitStats.Total;
    }
}
