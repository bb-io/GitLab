using Blackbird.Applications.Sdk.Common;

namespace Apps.Gitlab.Models.Commit.Requests;

public class DeleteFileRequest
{
    [Display("File path")]
    public string FilePath { get; set; }

    [Display("Commit message")]
    public string CommitMessage { get; set; }
}