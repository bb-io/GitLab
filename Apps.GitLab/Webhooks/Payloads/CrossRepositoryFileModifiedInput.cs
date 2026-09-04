using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GitLab.Webhooks.Payloads;

public class CrossRepositoryFileModifiedInput
{
    [Display("Repository IDs to watch", Description = "Optional repositories inside selected groups")]
    [DataSource(typeof(RepositoryDataHandler))]
    public IEnumerable<string>? RepositoryIds { get; set; }

    [Display("Repository IDs to ignore", Description = "Ignored repositories take precedence")]
    [DataSource(typeof(RepositoryDataHandler))]
    public IEnumerable<string>? IgnoredRepositoryIds { get; set; }

    [Display("Branches to watch", Description = "Case-sensitive regular expressions; any match includes push")]
    public IEnumerable<string>? BranchPatterns { get; set; }

    [Display("Branches to ignore", Description = "Case-sensitive regular expressions; any match ignores push")]
    public IEnumerable<string>? IgnoredBranchPatterns { get; set; }

    [Display("File patterns to watch", Description = "Glob patterns matched only against modified paths; bare filenames match at any depth")]
    public IEnumerable<string>? FilePatterns { get; set; }

    [Display("Commit messages to include", Description = "Case-sensitive regular expressions; message and file must match same commit")]
    public IEnumerable<string>? IncludedCommitMessagePatterns { get; set; }

    [Display("Commit messages to exclude", Description = "Case-sensitive regular expressions; exclusion wins for each commit")]
    public IEnumerable<string>? ExcludedCommitMessagePatterns { get; set; }

    [Display("Push user IDs to include", Description = "Filter whole push by GitLab push actor")]
    [DataSource(typeof(UsersDataHandler))]
    public IEnumerable<string>? UserIds { get; set; }

    [Display("Push user IDs to ignore", Description = "Ignored push users take precedence for whole push")]
    [DataSource(typeof(UsersDataHandler))]
    public IEnumerable<string>? IgnoredUserIds { get; set; }
}
