using Apps.Gitlab.Dtos;

namespace Apps.Gitlab.Models.Respository.Responses;

public class GetIssuesResponse
{
    public IEnumerable<IssueDto> Issues { get; set; }
}