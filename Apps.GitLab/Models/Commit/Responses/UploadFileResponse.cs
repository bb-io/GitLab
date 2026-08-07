using Apps.GitLab.Dtos;
using Apps.GitLab.Models.Responses;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GitLab.Models.Commit.Responses;

public record UploadFileResponse(
    CommitDto Commit,
    FileReference File,
    [property: Display("Number of units")] int NumberOfUnits,
    MetadataResponse? Metadata);
