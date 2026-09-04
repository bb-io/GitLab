namespace Apps.GitLab.Polling.Models;

public class PushPollingMemory
{
    public DateTimeOffset? LastCompletedScanUtc { get; set; }

    public List<ProcessedPushEventKey> RecentProcessedKeys { get; set; } = [];

    public string InputConfigurationFingerprint { get; set; } = string.Empty;
}

public sealed record ProcessedPushEventKey
{
    public int ProjectId { get; init; }

    public long EventId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
