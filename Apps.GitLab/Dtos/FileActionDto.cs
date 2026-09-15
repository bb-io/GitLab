using System.Text.Json.Serialization;

namespace Apps.GitLab.Dtos;

public class FileActionDto
{
    public FileActionDto(string action, string filePath, byte[]? file)
    {
        Action = action;
        FilePath = filePath;

        if(action != "delete")
        {
            ArgumentNullException.ThrowIfNull(file);
            Content = Convert.ToBase64String(file);
            Encoding = "base64";
        }
    }

    [JsonPropertyName("action")]
    public string Action { get; set; }

    [JsonPropertyName("file_path")]
    public string FilePath { get; set; }

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; set; }

    [JsonPropertyName("encoding")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Encoding { get; set; }
}
