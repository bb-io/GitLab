using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Filters.Constants;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Extensions;
using Blackbird.Filters.Shared;
using Blackbird.Filters.Transformations;
using Apps.GitLab.Models.Responses;

namespace Apps.GitLab.Utils;

public enum BlackbirdMetadataType
{
    Source,
    Target,
    Bilingual
}

public static class InteroperableFileHelper
{
    public static string GetContentId(string path, string repoPathWithNamespace)
    {
        return string.IsNullOrEmpty(repoPathWithNamespace)
            ? path
            : $"{repoPathWithNamespace}:{path}";
    }

    public static (string blobUrl, string editUrl) BuildUrls(string filePath, string branchName, string repoWebUrl)
    {
        var encodedPath = string.Join("/", filePath.Split('/').Select(Uri.EscapeDataString));
        var encodedBranchName = Uri.EscapeDataString(branchName);
        var blobUrl = $"{repoWebUrl}/-/blob/{encodedBranchName}/{encodedPath}";
        var editUrl = $"{repoWebUrl}/-/edit/{encodedBranchName}/{encodedPath}";
        return (blobUrl, editUrl);
    }

    public static async Task<(byte[] Content, BlackbirdMetadataType? MetadataType)> StripMetadata(
        Stream fileStream,
        string fileName,
        string contentType,
        Logger? logger)
    {
        var transformationResult = Transformation.Load(fileStream, fileName, contentType);
        var contentResult = transformationResult.Target();

        if (!contentResult.Success)
        {
            logger?.LogInformation($"Not a Blackbird interoperable file: {transformationResult.Error}", []);
            return (await ReadAllBytes(fileStream), null);
        }

        var contentWithoutMetadata = System.Text.Encoding.UTF8.GetBytes(
            contentResult.Value.ToStream(MetadataHandling.Exclude).ReadString());

        var metadataType = transformationResult.WasBilingual
            ? BlackbirdMetadataType.Bilingual
            : BlackbirdMetadataType.Target;

        return (contentWithoutMetadata, metadataType);
    }

    public static (Stream FileStream, string MimeType, string FileName, MetadataResponse? Metadata, int NumberOfUnits) AddMetadata(
        Stream fileStream,
        string fileName,
        string contentType,
        string path,
        string repoWebUrl,
        string branchName,
        string repoPathWithNamespace,
        string baseUrl,
        DateTimeOffset dateChanged,
        ProvenanceRecord reviewProvenance,
        BlackbirdMetadataType metadataType,
        Logger? logger)
    {
        var transformationResult = Transformation.Load(fileStream, fileName, contentType);
        var transformation = transformationResult.Value;

        if (transformation is null)
        {
            if (metadataType != BlackbirdMetadataType.Source)
                throw new PluginMisconfigurationException(
                    transformationResult.Error ?? "Unable to load the Blackbird interoperable file.");

            fileStream.Position = 0;
            logger?.LogInformation($"Not a Blackbird interoperable file: {transformationResult.Error}", []);
            return (fileStream, contentType, fileName, null, 0);
        }

        var numberOfUnits = transformation.GetUnits().Count();

        var (_, editUrl) = BuildUrls(path, branchName, repoWebUrl);
        var contentId = GetContentId(path, repoPathWithNamespace);

        var systemReference = metadataType == BlackbirdMetadataType.Source
            ? transformation.SourceSystemReference
            : transformation.TargetSystemReference;

        systemReference.ContentId = contentId;
        systemReference.ContentName = contentId;
        systemReference.AdminUrl = editUrl;
        systemReference.SystemName = "Gitlab";
        systemReference.SystemRef = baseUrl;

        transformation.MetaData.RemoveAll(metadata =>
            metadata.Category.Contains(Meta.Categories.Blackbird) &&
            metadata.Type == Meta.Types.DateChanged);
        transformation.DateChanged = dateChanged;
        transformation.Provenance.Review = reviewProvenance;

        var language = metadataType == BlackbirdMetadataType.Source
            ? transformation.SourceLanguage
            : transformation.TargetLanguage ?? transformation.SourceLanguage;
        var metadata = MetadataResponse.FromTransformation(
            language,
            dateChanged,
            systemReference,
            transformation);

        if (metadataType == BlackbirdMetadataType.Bilingual)
        {
            if (!transformationResult.WasBilingual)
                throw new PluginMisconfigurationException("The file is not a bilingual Blackbird interoperable file.");

            return (transformation.ToStream(), MediaTypes.Xliff2, transformation.BilingualFileName, metadata, numberOfUnits);
        }

        var contentResult = metadataType == BlackbirdMetadataType.Source
            ? transformation.Source()
            : transformation.Target();

        if (!contentResult.Success)
        {
            if (metadataType != BlackbirdMetadataType.Source)
                throw new PluginMisconfigurationException(contentResult.Error);

            fileStream.Position = 0;
            logger?.LogInformation($"Not a Blackbird interoperable file: {contentResult.Error}", []);
            return (fileStream, contentType, fileName, null, 0);
        }

        var content = contentResult.Value;
        content.SystemReference = systemReference;

        return (
            content.ToStream(),
            content.OriginalMediaType ?? contentType,
            content.OriginalName ?? fileName,
            metadata,
            numberOfUnits);
    }

    private static async Task<byte[]> ReadAllBytes(Stream stream)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);

        return buffer.ToArray();
    }
}
