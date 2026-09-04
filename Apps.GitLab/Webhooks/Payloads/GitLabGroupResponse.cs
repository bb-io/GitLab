using Newtonsoft.Json;

namespace Apps.GitLab.Webhooks.Payloads;

public class GitLabGroupResponse
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("full_path")]
    public string FullPath { get; set; } = string.Empty;

    [JsonProperty("parent_id")]
    public int? ParentId { get; set; }

    [JsonProperty("archived")]
    public bool Archived { get; set; }

    [JsonProperty("marked_for_deletion_on")]
    public DateTime? MarkedForDeletionOn { get; set; }

    [JsonProperty("plan")]
    public string? Plan { get; set; }
}
