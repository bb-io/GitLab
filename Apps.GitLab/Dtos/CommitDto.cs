using Blackbird.Applications.Sdk.Common;
using GitLabApiClient.Models.Commits.Responses;

namespace Apps.GitLab.Dtos;

public class CommitDto
{
    [Display("Commit ID")] public string Id { get; set; }

    public string Title { get; set; }

    public string Message { get; set; }

    public CommitDto()
    {
    }

    public CommitDto(Commit commit)
    {
        Id = commit.Id;
        Title = commit.Title;
        Message = commit.Message;
    }
}