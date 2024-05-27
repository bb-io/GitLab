using Blackbird.Applications.Sdk.Common;

namespace Apps.Gitlab.Models.PullRequest.Requests;

public class MergePullRequest
{
    [Display("Merge commit message")]
    public string? MergeCommitMessage { get; set; }
}