using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GitLab.Models.Commit.Requests;

public class PushFilesRequest
{
    [Display("Files")]
    public IEnumerable<FileReference> Files { get; set; } = [];

    [Display("Destination file paths", Description = "One repository path for each file, in the same order")]
    public IEnumerable<string> DestinationFilePaths { get; set; } = [];

    [Display("Commit message")]
    public string CommitMessage { get; set; } = string.Empty;
}
