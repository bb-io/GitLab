using Newtonsoft.Json;

namespace Apps.GitLab.Webhooks.Payloads;

public class WebhookResponse
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;

    [JsonProperty("push_events")]
    public bool PushEvents { get; set; }
}
