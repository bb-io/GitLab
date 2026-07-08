using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems;

namespace Apps.Gitlab.Models.Respository.Requests;

public class GetFileRequest
{
    [Display("File path")]
    [FileDataSource(typeof(FilePickerDataHandler))]
    public string FilePath { get; set; }

    [Display("Source language code", Description = "The language of the file used in later Actions.")]
    public string? LanguageCode { get; set; }

    [Display("Content ID", Description = "The ID of the content, used by Blacklake when diffing.")]
    public string? ContentId { get; set; }
}
