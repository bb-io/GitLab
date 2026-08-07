using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

using Apps.GitLab.Models.Responses;

namespace Apps.GitLab.Models.Respository.Responses;

public class GetFileResponse
{
    [Display("Full file path")]
    public string FilePath { get; set; }

    [Display("File")]
    public FileReference File { get; set; }

    [Display("File extension (e.g \".txt\")")]
    public string FileExtension { get; set; }

    [Display("Number of units")]
    public int NumberOfUnits { get; set; }

    [Display("Metadata")]
    public MetadataResponse? Metadata { get; set; }
}
