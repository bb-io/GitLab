using System.Text.Json.Serialization;

namespace Apps.GitLab.Dtos;

public class FileActionDto
{
    public FileActionDto(string action, string filePath, byte[] file)
    {
        Action = action;
        FilePath = filePath;

        if(action != "delete")
        {
            Content = Convert.ToBase64String(file);
            Encoding = "base64";
        }
    }

    [JsonPropertyName("action")]
    public string Action { get; set; }

    [JsonPropertyName("file_path")]
    public string FilePath { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }

    [JsonPropertyName("encoding")]
    public string Encoding { get; set; }
}