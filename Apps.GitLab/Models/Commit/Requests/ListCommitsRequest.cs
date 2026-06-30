using Blackbird.Applications.Sdk.Common;

namespace Apps.GitLab.Models.Commit.Requests;

public class ListCommitsRequest : SearchCommitsRequest
{
    [Display("Maximum results", Description = "Maximum number of matching commits to return")]
    public int? MaximumResults { get; set; } = 100;
}
