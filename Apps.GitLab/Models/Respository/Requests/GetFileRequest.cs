using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems;

namespace Apps.Gitlab.Models.Respository.Requests;

public class GetFileRequest
{
    [Display("File path")]
    [FileDataSource(typeof(FilePickerDataHandler))]
    public string FilePath { get; set; } = string.Empty;

    [Display("Commit ID (SHA)", Description = "Download the file at this commit instead of the selected or default branch. Leave empty to use the branch.")]
    public string? CommitId { get; set; }

    [Display("Content ID")]
    public string? ContentId { get; set; }
}
