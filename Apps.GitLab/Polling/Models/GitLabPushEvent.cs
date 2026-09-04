namespace Apps.GitLab.Polling.Models;

public class GitLabPushEvent
{
    public long Id { get; set; }

    public int ProjectId { get; set; }

    public string? ActionName { get; set; }

    public int? AuthorId { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public GitLabPushData? PushData { get; set; }
}

public class GitLabPushData
{
    public int CommitCount { get; set; }

    public int? RefCount { get; set; }

    public string? Action { get; set; }

    public string? RefType { get; set; }

    public string? CommitFrom { get; set; }

    public string? CommitTo { get; set; }

    public string? Ref { get; set; }

    public string? CommitTitle { get; set; }
}
