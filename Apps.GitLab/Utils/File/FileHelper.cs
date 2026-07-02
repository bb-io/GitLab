using System.Net.Mime;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Filters.Transformations;

namespace Apps.GitLab.Utils.File;

public static class FileHelper
{
    public static FileContentResult ProcessDownloadedFile(FileToProcess fileToProcess)
    {
        var filename = Path.GetFileName(fileToProcess.Path);
        if (!MimeTypes.TryGetMimeType(filename, out var mimeType))
            mimeType = MediaTypeNames.Application.Octet;

        var stream = new MemoryStream(Convert.FromBase64String(fileToProcess.Content));
        var transformationResult = Transformation.Load(stream, filename, mimeType);
        
        if (!transformationResult.Success)
        {
            stream.Position = 0;
            return new(stream, mimeType, filename);
        }

        string encodedPath = string.Join("/", fileToProcess.Path.Split('/').Select(Uri.EscapeDataString));
        string blobUrl = $"{fileToProcess.RepoWebUrl}/-/blob/{fileToProcess.BranchName}/{encodedPath}";
        string editUrl = $"{fileToProcess.RepoWebUrl}/-/edit/{fileToProcess.BranchName}/{encodedPath}";
        
        var transformation = transformationResult.Value!;

        transformation.SourceLanguage = filename.Split('.')[0];
        transformation.SourceSystemReference.ContentId = blobUrl;
        transformation.SourceSystemReference.AdminUrl = editUrl;
        transformation.SourceSystemReference.ContentName = filename;
        transformation.SourceSystemReference.SystemName = "GitLab";
        transformation.SourceSystemReference.SystemRef = "https://gitlab.com/";

        var sourceLoadResult = transformation.Source();
        if (!sourceLoadResult.Success)
            throw new PluginMisconfigurationException(sourceLoadResult.Error);
        
        return new(sourceLoadResult.Value.ToStream(), mimeType, filename);
    }
}