using Blackbird.Filters.Transformations;

namespace Apps.Gitlab.Extensions;

public static class TransformationExtensions
{
    public static (string blobUrl, string editUrl) BuildUrls(string filePath, string branchName, string repoWebUrl)
    {
        var encodedPath = string.Join("/", filePath.Split('/').Select(Uri.EscapeDataString));
        return ($"{repoWebUrl}/-/blob/{branchName}/{encodedPath}", $"{repoWebUrl}/-/edit/{branchName}/{encodedPath}");
    }
}