using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GitLab.Polling.Models;

public class FilesModifiedPollingInput
{
    [Display("Repositories to include", Description = "Repositories whose push activity should be checked")]
    [DataSource(typeof(RepositoryDataHandler))]
    public IEnumerable<string> RepositoryIds { get; set; } = [];

    [Display("Branches to watch", Description = "Case-sensitive regular expressions; any match includes the branch")]
    public IEnumerable<string>? BranchPatterns { get; set; }

    [Display("Branches to ignore", Description = "Case-sensitive regular expressions; ignore rules take precedence")]
    public IEnumerable<string>? IgnoredBranchPatterns { get; set; }

    [Display("File patterns to watch", Description = "Glob patterns for modified files; bare filenames match at any depth")]
    public IEnumerable<string>? FilePatterns { get; set; }

    [Display("Commit messages to include", Description = "Case-sensitive regular expressions; message and file must match the same commit")]
    public IEnumerable<string>? IncludedCommitMessagePatterns { get; set; }

    [Display("Commit messages to exclude", Description = "Case-sensitive regular expressions; exclusion takes precedence")]
    public IEnumerable<string>? ExcludedCommitMessagePatterns { get; set; }

    [Display("Pushers to watch", Description = "GitLab users whose pushes should be checked")]
    [DataSource(typeof(UsersDataHandler))]
    public IEnumerable<string>? PusherUserIds { get; set; }

    [Display("Pushers to ignore", Description = "GitLab users whose pushes should be ignored; ignore rules take precedence")]
    [DataSource(typeof(UsersDataHandler))]
    public IEnumerable<string>? IgnoredPusherUserIds { get; set; }
}
