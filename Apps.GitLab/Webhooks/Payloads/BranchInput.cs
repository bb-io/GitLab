using Blackbird.Applications.Sdk.Common;

namespace Apps.Gitlab.Webhooks.Payloads;

public class BranchInput
{
    [Display("Branch name")]
    public string? BranchName { get; set; }
}