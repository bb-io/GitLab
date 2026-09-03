using Blackbird.Applications.Sdk.Common;

namespace Apps.GitLab.Webhooks.Payloads;

public class CrossRepositoryFileModifiedResponse
{
    [Display("Repository ID")]
    public int RepositoryId { get; set; }

    [Display("Repository name with namespace")]
    public string RepositoryNameWithNamespace { get; set; } = string.Empty;

    [Display("Branch name")]
    public string BranchName { get; set; } = string.Empty;

    public IEnumerable<PushCommitOutput> Commits { get; set; } = [];

    [Display("File paths matched")]
    public IEnumerable<string> FilePathsMatched { get; set; } = [];

    [Display("Push user ID")]
    public int PushUserId { get; set; }

    [Display("Total commits count")]
    public int TotalCommitsCount { get; set; }

    [Display("Commits truncated")]
    public bool CommitsTruncated { get; set; }
}
