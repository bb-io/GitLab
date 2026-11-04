using Blackbird.Applications.Sdk.Common;

namespace Apps.GitLab.Models.Diagnostics;

public class DiagnoseFilesModifiedRequest
{
    [Display("Commit SHA", Description = "Full SHA of the commit to investigate")]
    public string CommitSha { get; set; } = string.Empty;

    [Display("Expected branch", Description = "Branch on which the commit was expected to trigger the event")]
    public string ExpectedBranch { get; set; } = string.Empty;
}
