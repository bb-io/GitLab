using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Gitlab.Models.PullRequest.Requests;

public class CreatePullRequest
{    public string Title { get; set; }

    public string? Description { get; set; }

    [Display("Head branch")]
    public string HeadBranch { get; set; }

    [Display("Base branch")]
    public string BaseBranch { get; set; }
}