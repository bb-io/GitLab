using Blackbird.Applications.Sdk.Common;

namespace Apps.Gitlab.Models.PullRequest.Responses;

public class IsPullMergedResponse
{
    [Display("Is merged")]
    public bool IsPullMerged { get; set; }
}