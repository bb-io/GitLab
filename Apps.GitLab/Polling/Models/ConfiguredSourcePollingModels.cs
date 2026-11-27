using Apps.GitLab.Configuration;

namespace Apps.GitLab.Polling.Models;

public class ConfiguredSourcePollingMemory
{
    public int ConfigurationRepositoryId { get; set; }

    public string ConfigurationBranch { get; set; } = string.Empty;

    public string ConfigurationPath { get; set; } = string.Empty;

    public Dictionary<int, ConfiguredRepositoryMemory> Repositories { get; set; } = [];
}

public class ConfiguredRepositoryMemory
{
    public DateTimeOffset? LastCheckedUtc { get; set; }

    public List<ConfiguredWatchMemory> Watches { get; set; } = [];
}

public class ConfiguredWatchMemory
{
    public string Branch { get; set; } = string.Empty;

    public string FileMask { get; set; } = string.Empty;

    public string SourceFilePath { get; set; } = string.Empty;

    public DateTimeOffset ActiveSinceUtc { get; set; }

    public DateTimeOffset? LastCheckedUtc { get; set; }

    public DateTimeOffset? LastCompletedScanUtc { get; set; }

    public Dictionary<long, DateTimeOffset> RecentEvents { get; set; } = [];
}

public class ConfiguredSourcePollingOutput : RepositoryConfigurationOutput
{
    public List<PushPollingOutput> Pushes { get; set; } = [];
}
