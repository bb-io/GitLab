using Apps.GitLab.Dtos;
using Apps.GitLab.Models.Responses;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GitLab.Models.Commit.Responses;

public record UploadFilesResponse(
    CommitDto Commit,
    IEnumerable<UploadedFileResponse> Files);

public record UploadedFileResponse(
    FileReference File,
    [property: Display("Destination file path")] string DestinationFilePath,
    [property: Display("Number of units")] int NumberOfUnits,
    MetadataResponse? Metadata);
