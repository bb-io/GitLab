using Blackbird.Applications.Sdk.Common;

namespace Apps.Gitlab.Models.Respository.Requests;

public class GetFileRequest
{
    [Display("File path")]
    public string FilePath { get; set; }
}