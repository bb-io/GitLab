using Blackbird.Applications.Sdk.Common;

namespace Apps.GitLab.Polling.Models;

public class FilesModifiedPollingResponse
{
    public IEnumerable<PushPollingOutput> Pushes { get; set; } = [];
}

public class PushPollingOutput
{
    [Display("Repository ID")]
    public int RepositoryId { get; set; }

    [Display("Repository name with namespace")]
    public string RepositoryNameWithNamespace { get; set; } = string.Empty;

    [Display("Branch name")]
    public string BranchName { get; set; } = string.Empty;

    public IEnumerable<PushCommitOutput> Commits { get; set; } = [];

    [Display("Push time")]
    public DateTime PushTime { get; set; }

    [Display("File paths matched")]
    public IEnumerable<string> FilePathsMatched { get; set; } = [];

    [Display("Pusher user ID")]
    public int PusherUserId { get; set; }
}

public class PushCommitOutput
{
    public string Hash { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    [Display("Author name")]
    public string AuthorName { get; set; } = string.Empty;

    [Display("Author email")]
    public string AuthorEmail { get; set; } = string.Empty;
}
