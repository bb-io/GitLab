using Newtonsoft.Json;

namespace Apps.GitLab.Webhooks.Payloads;

public class WebhookResponse
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("push_events")]
    public bool PushEvents { get; set; }
}
