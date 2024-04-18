using Blackbird.Applications.Sdk.Common;

namespace Apps.Gitlab.Models.Commit.Requests;

public class UpdateFileRequest : PushFileRequest
{
    [Display("File ID (Sha)")]
    public string? FileId { get; set; }
}