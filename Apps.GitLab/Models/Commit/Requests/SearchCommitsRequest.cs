using Blackbird.Applications.Sdk.Common;

namespace Apps.GitLab.Models.Commit.Requests;

public class SearchCommitsRequest
{
    [Display("Authors to include", Description = "Author names or emails to include")]
    public List<string>? AuthorsToInclude { get; set; }

    [Display("Authors to exclude", Description = "Author names or emails to exclude")]
    public List<string>? AuthorsToExclude { get; set; }

    [Display("Commit after", Description = "Only commits after or on this date")]
    public DateTime? CommitAfter { get; set; }

    [Display("Commit before", Description = "Only commits before or on this date")]
    public DateTime? CommitBefore { get; set; }

    [Display("Commit message contains")]
    public string? CommitMessageContains { get; set; }

    [Display("File path", Description = "Only commits touching this file path")]
    public string? FilePath { get; set; }
}
