using Newtonsoft.Json;

namespace Apps.GitLab.Dtos;

public class RepositoryInfo
{
    [JsonProperty("default_branch")]
    public string? DefaultBranch { get; set; }
}
