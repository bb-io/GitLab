using Apps.Gitlab.Dtos;
using Blackbird.Applications.Sdk.Common;

namespace Apps.Gitlab.Models.PullRequest.Responses;

public class ListPullRequestsResponse
{
    [Display("Pull requests")]
    public IEnumerable<PullRequestDto> PullRequests { get; set; }
}