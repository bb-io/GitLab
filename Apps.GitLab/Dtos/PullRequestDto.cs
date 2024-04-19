using GitLabApiClient.Models.MergeRequests.Responses;

namespace Apps.Gitlab.Dtos;

public class PullRequestDto
{
    public PullRequestDto(MergeRequest source)
    {
        Id = source.Id.ToString();
        Title = source.Title;
        Description = source.Description;
        Author = source.Author.Username;
        Url = source.WebUrl;
    }
    public string Id { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public string Author { get; set; }

    public string Url { get; set; }
}