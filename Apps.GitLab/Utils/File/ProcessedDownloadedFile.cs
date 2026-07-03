namespace Apps.GitLab.Utils.File;

public record ProcessedDownloadedFile(Stream FileStream, string MimeType, string FileName);