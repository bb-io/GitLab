namespace Apps.GitLab.Utils.File;

public record DownloadedFile(string Content, string Path, string RepoWebUrl, string BranchName, string BaseUrl);