using Apps.GitLab.Dtos;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.GitLab.Models.Commit.Responses;

public record UploadFileResponse(CommitDto Commit, FileReference File);