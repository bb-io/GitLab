using Newtonsoft.Json;

namespace Apps.GitLab.Models.Respository.Responses;

public class RepositoryFileResponse
{
    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;

    [JsonProperty("file_path")]
    public string FilePath { get; set; } = string.Empty;

    [JsonProperty("file_name")]
    public string FileName { get; set; } = string.Empty;
    
    
}

