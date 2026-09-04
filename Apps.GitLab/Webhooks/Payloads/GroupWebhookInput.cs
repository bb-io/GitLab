using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GitLab.Webhooks.Payloads;

public class GroupWebhookInput
{
    [Display("Groups to watch", Description = "Groups whose repositories and descendant-group repositories should be watched")]
    [DataSource(typeof(GroupDataHandler))]
    public IEnumerable<string> GroupIds { get; set; } = [];
}
