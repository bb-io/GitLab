using Apps.Gitlab.DataSourceHandlers;
using Apps.GitLab.DataSourceHandlers.EnumHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
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

    [Display("Content name")]
    public string? ContentName { get; set; }

    [Display("Output file type", Description = "Output format. Defaults to Original.")]
    [StaticDataSource(typeof(OutputFileTypeDataHandler))]
    public string? OutputFileType { get; set; }
}
