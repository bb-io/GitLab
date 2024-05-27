using Blackbird.Applications.Sdk.Common;

namespace Apps.Gitlab.Models.PullRequest.Requests;

public class ListPullFilesRequest
{
    [Display("Pull request number")]
    public string PullRequestNumber { get; set; }
}