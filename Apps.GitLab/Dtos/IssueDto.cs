using GitLabApiClient.Models.Issues.Responses;

namespace Apps.Gitlab.Dtos;

public class IssueDto
{
    public IssueDto(Issue source)
    {
        Title = source.Title;
        Description = source.Description;
        Author = source.Author.Username;
        Url = source.WebUrl;
    }
    public string Title { get; set; }

    public string Description { get; set; }

    public string Author { get; set; }

    public string Url { get; set; }
}