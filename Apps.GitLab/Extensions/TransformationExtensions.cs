using Blackbird.Filters.Transformations;

namespace Apps.Gitlab.Extensions;

public static class TransformationExtensions
{
    private static (string blobUrl, string editUrl) BuildUrls(string filePath, string branchName, string repoWebUrl)
    {
        var encodedPath = string.Join("/", filePath.Split('/').Select(Uri.EscapeDataString));
        return ($"{repoWebUrl}/-/blob/{branchName}/{encodedPath}", $"{repoWebUrl}/-/edit/{branchName}/{encodedPath}");
    }

    public static Transformation AddSourceMetadata(
        this Transformation t, 
        string filePath, 
        string fileName, 
        string branchName, 
        string repoWebUrl,
        string baseUrl)
    {
        var (blobUrl, editUrl) = BuildUrls(filePath, branchName, repoWebUrl);
        t.SourceLanguage = fileName.Split('.')[0];
        t.SourceSystemReference.ContentId = blobUrl;
        t.SourceSystemReference.AdminUrl = editUrl;
        t.SourceSystemReference.ContentName = fileName;
        t.SourceSystemReference.SystemName = "GitLab";
        t.SourceSystemReference.SystemRef = baseUrl;
        return t;
    }

    public static Transformation AddTargetMetadata(
        this Transformation t, 
        string filePath, 
        string fileName, 
        string branchName, 
        string repoWebUrl,
        string baseUrl)
    {
        var (blobUrl, editUrl) = BuildUrls(filePath, branchName, repoWebUrl);
        t.TargetLanguage = fileName.Split('.')[0];
        t.TargetSystemReference.ContentId = blobUrl;
        t.TargetSystemReference.AdminUrl = editUrl;
        t.TargetSystemReference.ContentName = fileName;
        t.TargetSystemReference.SystemName = "GitLab";
        t.TargetSystemReference.SystemRef = baseUrl;
        return t;
    }
}