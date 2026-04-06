using Newtonsoft.Json;

namespace Apps.GitLab.Webhooks.Payloads;

public class CreateWebhookRequest
{
    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;

    [JsonProperty("push_events")]
    public bool PushEvents { get; set; }

    [JsonProperty("push_events_branch_filter")]
    public string? PushEventsBranchFilter { get; set; }
}