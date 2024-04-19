using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Gitlab.Models.PullRequest.Requests;

public class GetPullRequest
{
    [Display("Merge request ID")]
    public string PullRequestId { get; set; }
}