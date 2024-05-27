using Blackbird.Applications.Sdk.Common;

namespace Apps.Gitlab.Models.Commit.Requests;

public class GetCommitRequest
{
    [Display("Commit ID (Sha)")]
    public string CommitId { get; set; }
}