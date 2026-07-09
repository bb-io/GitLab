namespace Apps.GitLab.Utils.File;

public record UploadedFile(Stream Stream, string Name, string ContentType);