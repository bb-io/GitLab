using Blackbird.Applications.Sdk.Common;
using GitLabApiClient.Models.MergeRequests.Responses;

namespace Apps.Gitlab.Models.PullRequest.Responses;

public class ListPullRequestsResponse
{
    [Display("Merge requests")]
    public IEnumerable<MergeRequest> PullRequests { get; set; }
}