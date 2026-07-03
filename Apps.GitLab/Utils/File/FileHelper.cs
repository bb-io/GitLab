using System.Net.Mime;
using Apps.Gitlab.Extensions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Transformations;

namespace Apps.GitLab.Utils.File;

public static class FileHelper
{
    public static ProcessedDownloadedFile ProcessDownloadedFile(DownloadedFile downloadedFile)
    {
        var filename = Path.GetFileName(downloadedFile.Path);
        if (!MimeTypes.TryGetMimeType(filename, out var mimeType))
            mimeType = MediaTypeNames.Application.Octet;

        var stream = new MemoryStream(Convert.FromBase64String(downloadedFile.Content));
        var transformationResult = Transformation.Load(stream, filename, mimeType);
        
        if (!transformationResult.Success)
        {
            stream.Position = 0;
            return new(stream, mimeType, filename);
        }
        
        var transformation = transformationResult.Value.AddSourceMetadata(
            downloadedFile.Path, 
            filename, 
            downloadedFile.BranchName, 
            downloadedFile.RepoWebUrl);

        var sourceLoadResult = transformation.Source();
        if (!sourceLoadResult.Success)
            throw new PluginMisconfigurationException(sourceLoadResult.Error);
        
        return new(sourceLoadResult.Value.ToStream(), mimeType, filename);
    }
    
    public static async Task<ProcessedUploadedFile> ProcessUploadFile(UploadedFile uploadedFile)
    {
        var transformationResult = Transformation.Load(uploadedFile.Stream, uploadedFile.Name, uploadedFile.ContentType);
        bool isJson = uploadedFile.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        
        if (transformationResult.Success && isJson)
        {
            var sourceLoadResult = transformationResult.Value.Source();
            if (!sourceLoadResult.Success)
                throw new PluginMisconfigurationException(sourceLoadResult.Error);

            var bytes = await sourceLoadResult.Value.ToStream(MetadataHandling.Exclude).GetByteData();
            return new(bytes, null);
        }
        if (transformationResult.Success)
        {
            var targetLoadResult = transformationResult.Value.Target();
            if (!targetLoadResult.Success)
                throw new PluginMisconfigurationException(targetLoadResult.Error);

            var bytes = await targetLoadResult.Value.ToStream(MetadataHandling.Exclude).GetByteData();
            return new(bytes, transformationResult.Value);
        }
        
        var raw = await uploadedFile.Stream.GetByteData();
        return new(raw, null);     
    }
}