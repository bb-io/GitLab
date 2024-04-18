using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Gitlab.Models.Branch.Requests;

public class MergeBranchRequest
{
    [Display("Base branch")]
    public string BaseBranch { get; set; }

    [Display("Head branch")]
    public string HeadBranch { get; set; }

    [Display("Commit message")]
    public string CommitMessage { get; set; }
}