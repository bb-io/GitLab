namespace Apps.GitLab.Utils.File;

public record FileContentResult(Stream FileStream, string MimeType, string FileName);