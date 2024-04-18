using Apps.Gitlab.Dtos;
using Blackbird.Applications.Sdk.Common;

namespace Apps.Gitlab.Models.Respository.Responses;

public class GetPullRequestsResponse
{
    [Display("Pull requests")]
    public IEnumerable<PullRequestDto> PullRequests { get; set; }
}