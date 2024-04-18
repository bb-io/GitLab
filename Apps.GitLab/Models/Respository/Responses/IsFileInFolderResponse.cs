using Blackbird.Applications.Sdk.Common;

namespace Apps.Gitlab.Models.Respository.Responses;

public class IsFileInFolderResponse
{
    [Display("Is file in folder")]
    public int IsFileInFolder { get; set; }
}