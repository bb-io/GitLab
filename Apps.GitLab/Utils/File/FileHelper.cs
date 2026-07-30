using Apps.Gitlab.Extensions;
using Apps.GitLab.Constants;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Blackbird.Filters.Bilingual.Xliff1;
using Blackbird.Filters.Bilingual.Xliff2;
using Blackbird.Filters.Constants;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Transformations;
using System.Net.Mime;
using System.Text;

namespace Apps.GitLab.Utils.File;

public static class FileHelper
{
    public static ProcessedDownloadedFile ProcessDownloadedFile(
        DownloadedFile downloadedFile,
        Logger? logger,
        string? language,
        string? contentId,
        string? contentName,
        string? outputFileType = null,
        string? targetLocale = null)
    {
        outputFileType ??= OutputFileTypes.Original;
        if (outputFileType is not OutputFileTypes.Original
            and not OutputFileTypes.Xliff1
            and not OutputFileTypes.Xliff2)
        {
            throw new PluginMisconfigurationException($"Unsupported output file type: {outputFileType}");
        }

        var filename = Path.GetFileName(downloadedFile.Path);
        if (!MimeTypes.TryGetMimeType(filename, out var mimeType))
            mimeType = MediaTypeNames.Application.Octet;

        var stream = new MemoryStream(Convert.FromBase64String(downloadedFile.Content));
        var fileResult = Transformation.Load(stream, filename, mimeType).Source();
        
        if (!fileResult.Success)
        {
            stream.Position = 0;
            if (outputFileType != OutputFileTypes.Original)
            {
                throw new PluginMisconfigurationException(
                    $"File could not be converted to the selected XLIFF format: {fileResult.Error}");
            }

            logger?.LogInformation($"Not a Blackbird interoperable file: {fileResult.Error}", []);
            return new(stream, mimeType, filename);
        }

        var fileContent = fileResult.Value;
        var (blobUrl, editUrl) = TransformationExtensions.BuildUrls(downloadedFile.Path, downloadedFile.BranchName, downloadedFile.RepoWebUrl);

        fileContent.Language = language;
        fileContent.SystemReference.ContentId = contentId;
        fileContent.SystemReference.AdminUrl = editUrl;
        fileContent.SystemReference.ContentName = string.IsNullOrWhiteSpace(contentName) ? filename : contentName;
        fileContent.SystemReference.SystemName = "Gitlab";
        fileContent.SystemReference.SystemRef = "https://gitlab.com/";

        if (outputFileType == OutputFileTypes.Original)
            return new(fileContent.ToStream(), mimeType, filename);

        var transformation = fileContent.CreateTransformation(targetLocale);
        if (outputFileType == OutputFileTypes.Xliff1)
        {
            var metadata = new[]
            {
                (Meta.Direction.Source + Meta.Types.ContentId, fileContent.SystemReference.ContentId),
                (Meta.Direction.Source + Meta.Types.ContentName, fileContent.SystemReference.ContentName),
                (Meta.Direction.Source + Meta.Types.AdminUrl, fileContent.SystemReference.AdminUrl),
                (Meta.Direction.Source + Meta.Types.PublicUrl, fileContent.SystemReference.PublicUrl),
                (Meta.Direction.Source + Meta.Types.SystemName, fileContent.SystemReference.SystemName),
                (Meta.Direction.Source + Meta.Types.SystemRef, fileContent.SystemReference.SystemRef)
            };

            transformation.MetaData.AddRange(metadata
                .Where(x => x.Item2 is not null)
                .Select(x => new Metadata(x.Item1, x.Item2!)
                {
                    Category = [Meta.Categories.Blackbird]
                }));
        }

        var xliff = outputFileType == OutputFileTypes.Xliff1
            ? Xliff1Serializer.Serialize(transformation)
            : Xliff2Serializer.Serialize(transformation, Xliff2Version.Xliff22);
        var xliffMimeType = outputFileType == OutputFileTypes.Xliff1
            ? "application/x-xliff+xml"
            : OutputFileTypes.Xliff2;

        return new(new MemoryStream(Encoding.UTF8.GetBytes(xliff)), xliffMimeType, transformation.BilingualFileName);
    }
}
