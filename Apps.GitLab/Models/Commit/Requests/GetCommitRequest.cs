using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Gitlab.Models.Commit.Requests;

public class GetCommitRequest
{
    [Display("Commit ID")]
    public string CommitId { get; set; }
}