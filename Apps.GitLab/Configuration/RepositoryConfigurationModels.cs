using System.Text.Json;
using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems;

namespace Apps.GitLab.Configuration;

public class ConfigurationFileInput
{
    [Display("Configuration file path")]
    [FileDataSource(typeof(ConfigurationFilePicker))]
    public string FilePath { get; set; } = string.Empty;
}

public class ConfigurationRepositoryInput
{
    [Display("Configuration repository")]
    [DataSource(typeof(RepositoryDataHandler))]
    public string RepositoryId { get; set; } = string.Empty;
}

public class ConfigurationBranchInput
{
    [Display("Configuration branch")]
    [DataSource(typeof(ConfigurationBranchDataHandler))]
    public string Name { get; set; } = string.Empty;
}

public class ConfigurationPathInput
{
    [Display("Configuration path")]
    [FileDataSource(typeof(ConfigurationPathDataHandler))]
    public string FilePath { get; set; } = string.Empty;
}

public class GetRepositoryConfigurationRequest
{
    [Display("Search repository ID")]
    [DataSource(typeof(RepositoryDataHandler))]
    public string? SearchRepositoryId { get; set; }

    [Display("Search branch")]
    public string? Branch { get; set; }

    [Display("Search file mask")]
    public string? FileMask { get; set; }
}

public class SearchRepositoryConfigurationsRequest
{
    [Display("Search repository ID")]
    [DataSource(typeof(RepositoryDataHandler))]
    public string? SearchRepositoryId { get; set; }

    [Display("Search branch")]
    public string? Branch { get; set; }

    [Display("Search file mask")]
    public string? FileMask { get; set; }

    [Display("Source locale")]
    public string? SourceLocale { get; set; }

    [Display("Target locales")]
    public IEnumerable<string>? TargetLocales { get; set; }

    [Display("Contains metadata", Description = "Text to find in metadata field names or values")]
    public string? ContainsMetadata { get; set; }
}

public class RepositoryConfigurationOutput
{
    [Display("Repository ID")]
    public int RepositoryId { get; set; }

    public string Branch { get; set; } = string.Empty;

    [Display("File mask")]
    public string FileMask { get; set; } = string.Empty;

    [Display("Source locale")]
    public string SourceLocale { get; set; } = string.Empty;

    [Display("Target locales")]
    public List<string> TargetLocales { get; set; } = [];

    public bool Enabled { get; set; }

    [Display("Source file path")]
    public string SourceFilePath { get; set; } = string.Empty;

    [Display("Metadata file")]
    public FileReference Metadata { get; set; } = null!;
}

public class RepositoryConfigurationsOutput
{
    public List<RepositoryConfigurationOutput> Configurations { get; set; } = [];
}

internal readonly record struct ConfigurationKey(int RepositoryId, string Branch, string FileMask);

internal sealed record RepositoryConfiguration(
    ConfigurationKey Key,
    int EntryNumber,
    string SourceLocale,
    List<string> TargetLocales,
    bool Enabled,
    string SourceFilePath,
    JsonElement Metadata);
