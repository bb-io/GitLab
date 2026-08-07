using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Filters.Constants;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Extensions;
using Blackbird.Filters.Transformations;

namespace Apps.GitLab.Utils;

public static class InteroperableFileHelper
{
    public static string GetContentId(string path, string repoNameWithNamespace)
    {
        return string.IsNullOrEmpty(repoNameWithNamespace)
            ? path
            : $"{repoNameWithNamespace}:{path}";
    }

    public static (string blobUrl, string editUrl) BuildUrls(string filePath, string branchName, string repoWebUrl)
    {
        var encodedPath = string.Join("/", filePath.Split('/').Select(Uri.EscapeDataString));
        return ($"{repoWebUrl}/-/blob/{branchName}/{encodedPath}", $"{repoWebUrl}/-/edit/{branchName}/{encodedPath}");
    }

    public static (Stream FileStream, string MimeType, string FileName) AddMetadataToDownloadedFile(
        string content,
        string path,
        string repoWebUrl,
        string branchName,
        string repoNameWithNamespace,
        string baseUrl,
        Logger? logger)
    {
        var filename = Path.GetFileName(path);
        var mimeType = MimeTypes.GetMimeType(filename);
        var stream = new MemoryStream(Convert.FromBase64String(content));

        var transformationLoadResult = Transformation.Load(stream, filename, mimeType).Source();
        
        if (!transformationLoadResult.Success)
        {
            stream.Position = 0;
            logger?.LogInformation($"Not a Blackbird interoperable file: {transformationLoadResult.Error}", []);
            return new(stream, mimeType, filename);
        }

        var fileContent = transformationLoadResult.Value;
        var (_, editUrl) = BuildUrls(path, branchName, repoWebUrl);
        var contentId = GetContentId(path, repoNameWithNamespace);

        var systemReference = fileContent.SystemReference!;
        systemReference.ContentId = contentId;
        systemReference.ContentName = contentId;
        systemReference.AdminUrl = editUrl;
        systemReference.SystemName = "Gitlab";
        systemReference.SystemRef = baseUrl;
        
        return new(fileContent.ToStream(), mimeType, filename);
    }

    public static (byte[] Content, Stream? FileStream, string? MimeType, string? FileName) ExtractMetadataForUploadedFile(
        Stream fileStream,
        string fileName,
        string contentType,
        string destinationFilePath,
        string branchName,
        string repoWebUrl,
        string repoNameWithNamespace,
        string baseUrl,
        Logger? logger)
    {
        var transformationResult = Transformation.Load(fileStream, fileName, contentType);
        var contentResult = transformationResult.Target();

        if (!contentResult.Success)
        {
            logger?.LogInformation($"Not a Blackbird interoperable file: {transformationResult.Error}", []);
            return (System.Text.Encoding.UTF8.GetBytes(fileStream.ReadString()), null, null, null);
        }

        var contentWithoutMetadata = System.Text.Encoding.UTF8.GetBytes(
            contentResult.Value.ToStream(MetadataHandling.Exclude).ReadString());

        var (_, editUrl) = BuildUrls(destinationFilePath, branchName, repoWebUrl);
        var contentId = GetContentId(destinationFilePath, repoNameWithNamespace);

        var transformation = transformationResult.Value!;
        var targetSystemReference = transformation.TargetSystemReference!;
        targetSystemReference.ContentId = contentId;
        targetSystemReference.ContentName = contentId;
        targetSystemReference.AdminUrl = editUrl;
        targetSystemReference.SystemName = "Gitlab";
        targetSystemReference.SystemRef = baseUrl;

        if (transformationResult.WasBilingual)
        {
            return (
                contentWithoutMetadata,
                transformation.ToStream(),
                MediaTypes.Xliff2,
                transformation.BilingualFileName);
        }

        var targetResult = transformation.Target();
        if (!targetResult.Success)
            throw new PluginMisconfigurationException(targetResult.Error);

        var target = targetResult.Value;
        target.SystemReference = targetSystemReference;

        return (contentWithoutMetadata, target.ToStream(), target.OriginalMediaType, target.OriginalName);
    }
}
