using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Gitlab.Models.PullRequest.Requests;

public class MergePullRequest
{
    [Display("Pull request number")]
    public string PullRequestNumber { get; set; }
}