namespace Apps.GitLab.Polling.Models;

public class GitLabCompareResponse
{
    public List<GitLabCompareCommit> Commits { get; set; } = [];

    public bool CompareTimeout { get; set; }

    public bool CompareSameRef { get; set; }
}

public class GitLabCompareCommit
{
    public string Id { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;

    public string AuthorEmail { get; set; } = string.Empty;
}

public class GitLabCommitDiff
{
    public string NewPath { get; set; } = string.Empty;

    public string OldPath { get; set; } = string.Empty;

    public bool? NewFile { get; set; }

    public bool? DeletedFile { get; set; }

    public bool? RenamedFile { get; set; }

    public bool? Collapsed { get; set; }

    public bool? TooLarge { get; set; }
}
