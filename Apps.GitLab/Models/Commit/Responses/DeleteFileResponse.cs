using Blackbird.Applications.Sdk.Common;

namespace Apps.GitLab.Models.Commit.Responses;

public class DeleteFileResponse
{
    [Display("Commit ID")]
    public string CommitId { get; set; }
    
    public string Title { get; set; }
    
    public string Message { get; set; }
}