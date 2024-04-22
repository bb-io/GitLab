using Apps.Gitlab.DataSourceHandlers;
using Apps.GitLab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Gitlab.Models.PullRequest.Requests;

public class GetPullRequest
{
    [Display("Merge request ID")]
    [DataSource(typeof(MergeRequestDataHandler))]
    public string PullRequestId { get; set; }
}