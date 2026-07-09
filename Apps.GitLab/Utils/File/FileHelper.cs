using Apps.Gitlab.Extensions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Transformations;
using System.Net.Mime;

namespace Apps.GitLab.Utils.File;

public static class FileHelper
{
    public static ProcessedDownloadedFile ProcessDownloadedFile(DownloadedFile downloadedFile, Logger? logger, string? language, string? contentId)
    {
        var filename = Path.GetFileName(downloadedFile.Path);
        if (!MimeTypes.TryGetMimeType(filename, out var mimeType))
            mimeType = MediaTypeNames.Application.Octet;

        var stream = new MemoryStream(Convert.FromBase64String(downloadedFile.Content));
        var fileResult = Transformation.Load(stream, filename, mimeType).Source();
        
        if (!fileResult.Success)
        {
            stream.Position = 0;
            logger?.LogInformation($"Not a Blackbird interoperable file: {fileResult.Error}", []);
            return new(stream, mimeType, filename);
        }

        var fileContent = fileResult.Value;
        var (blobUrl, editUrl) = TransformationExtensions.BuildUrls(downloadedFile.Path, downloadedFile.BranchName, downloadedFile.RepoWebUrl);

        fileContent.Language = language;
        fileContent.SystemReference.ContentId = contentId;
        fileContent.SystemReference.AdminUrl = editUrl;
        fileContent.SystemReference.ContentName = filename;
        fileContent.SystemReference.SystemName = "Gitlab";
        fileContent.SystemReference.SystemRef = "https://gitlab.com/";
        
        return new(fileContent.ToStream(), mimeType, filename);
    }
}