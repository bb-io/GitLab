using Apps.Gitlab;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.GitLab.Configuration;
using Apps.GitLab.Polling.Models;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Polling;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;

namespace Apps.GitLab.Polling;

[PollingEventList("Configured source files")]
public class ConfiguredSourcePollingList : BaseInvocable
{
    private const int FlightLimit = 20;
    private static readonly TimeSpan Overlap = TimeSpan.FromHours(24);
    private static readonly TimeSpan ScanTimeLimit = TimeSpan.FromMinutes(5);

    private readonly BlackbirdGitlabClient _client;
    private readonly RepositoryConfigurationReader _reader;
    private readonly Func<DateTimeOffset> _utcNow;

    public ConfiguredSourcePollingList(InvocationContext context, IFileManagementClient fileManagement)
        : this(context, fileManagement, new BlackbirdGitlabClient(context.AuthenticationCredentialsProviders),
            () => DateTimeOffset.UtcNow)
    {
    }

    internal ConfiguredSourcePollingList(InvocationContext context, IFileManagementClient fileManagement,
        BlackbirdGitlabClient client, Func<DateTimeOffset> utcNow) : base(context)
    {
        _client = client;
        _utcNow = utcNow;
        _reader = new RepositoryConfigurationReader(client, fileManagement, Warn);
    }

    [PollingEvent("On source files modified across repositories",
        Description = "Starts a flight when a source file in the configuration is modified")]
    [MultipleEvents]
    public async Task<PollingEventResponse<ConfiguredSourcePollingMemory, List<ConfiguredSourcePollingOutput>>>
        OnSourceFilesModifiedAcrossRepos(
            PollingEventRequest<ConfiguredSourcePollingMemory> request,
            [PollingEventParameter] GetRepositoryRequest repository,
            [PollingEventParameter] GetBranchRequest branch,
            [PollingEventParameter] ConfigurationFileInput file)
    {
        var upper = _utcNow().ToUniversalTime();
        using var deadline = new CancellationTokenSource(ScanTimeLimit);
        var token = deadline.Token;
        var repositoryId = RepositoryConfigurationReader.ParseRepositoryId(repository.RepositoryId);
        var entries = await _reader.Read(repositoryId, branch.Name, file.FilePath, token);
        var previous = request.Memory;
        if (previous?.ConfigurationRepositoryId != repositoryId ||
            previous.ConfigurationBranch != branch.Name || previous.ConfigurationPath != file.FilePath)
            previous = null;

        var memory = new ConfiguredSourcePollingMemory
        {
            ConfigurationRepositoryId = repositoryId,
            ConfigurationBranch = branch.Name,
            ConfigurationPath = file.FilePath
        };
        var groups = entries.Where(entry => entry.Enabled).GroupBy(entry => entry.Key.RepositoryId);
        var entriesByKey = new Dictionary<ConfigurationKey, RepositoryConfiguration>();
        foreach (var group in groups)
        {
            var oldRepository = previous?.Repositories.GetValueOrDefault(group.Key);
            var oldWatches = oldRepository?.Watches.ToDictionary(watch => (watch.Branch, watch.FileMask));
            var newRepository = new ConfiguredRepositoryMemory { LastCheckedUtc = oldRepository?.LastCheckedUtc };
            memory.Repositories.Add(group.Key, newRepository);

            foreach (var entry in group)
            {
                var key = entry.Key;
                entriesByKey.Add(key, entry);
                var oldWatch = oldWatches?.GetValueOrDefault((key.Branch, key.FileMask));
                newRepository.Watches.Add(CopyWatch(entry, oldWatch, upper));
            }
        }

        var flights = new List<ConfiguredSourcePollingOutput>();
        foreach (var (projectId, projectMemory) in memory.Repositories
                     .OrderBy(pair => pair.Value.LastCheckedUtc).ThenBy(pair => pair.Key))
        {
            token.ThrowIfCancellationRequested();
            projectMemory.LastCheckedUtc = upper;
            var scan = new ConfiguredRepositoryScan(_client, projectId, upper,
                projectMemory.Watches.Min(ScanLowerBound), Warn, token);
            if (!await scan.CheckRepository())
                continue;

            foreach (var watch in projectMemory.Watches.OrderBy(watch => watch.LastCheckedUtc)
                         .ThenBy(watch => watch.Branch, StringComparer.Ordinal)
                         .ThenBy(watch => watch.FileMask, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                watch.LastCheckedUtc = upper;

                try
                {
                    if (!await scan.CheckSource(watch.Branch, watch.SourceFilePath))
                        continue;

                    var (pushes, completedEvents) = await GetMatchingPushes(scan, watch, upper);
                    if (pushes.Count > 0)
                    {
                        var entry = entriesByKey[new ConfigurationKey(projectId, watch.Branch, watch.FileMask)];
                        var output = new ConfiguredSourcePollingOutput { Pushes = pushes };
                        await _reader.ToOutput(entry, output);
                        flights.Add(output);
                    }

                    watch.LastCompletedScanUtc = upper;
                    watch.RecentEvents = watch.RecentEvents.Concat(completedEvents)
                        .Where(item => item.Value >= upper - Overlap)
                        .ToDictionary();
                }
                catch (ConfiguredRepositoryUnavailableException)
                {
                    Warn($"Repository {projectId} was not found or is inaccessible. Check the connection's read permissions.");
                    break;
                }
                catch (PluginApplicationException exception)
                {
                    Warn($"Could not check '{watch.SourceFilePath}' on branch '{watch.Branch}' in repository {projectId}. {exception.Message}");
                    continue;
                }

                if (flights.Count == FlightLimit)
                    break;
            }

            if (flights.Count == FlightLimit)
                break;
        }

        token.ThrowIfCancellationRequested();
        return new PollingEventResponse<ConfiguredSourcePollingMemory, List<ConfiguredSourcePollingOutput>>
        {
            FlyBird = flights.Count != 0,
            Result = flights.Count != 0 ? flights : null!,
            Memory = memory
        };
    }

    private static ConfiguredWatchMemory CopyWatch(
        RepositoryConfiguration entry, ConfiguredWatchMemory? previous, DateTimeOffset upper)
    {
        if (previous is null || previous.SourceFilePath != entry.SourceFilePath || previous.ActiveSinceUtc > upper)
            return new ConfiguredWatchMemory
            {
                Branch = entry.Key.Branch,
                FileMask = entry.Key.FileMask,
                SourceFilePath = entry.SourceFilePath,
                ActiveSinceUtc = upper
            };

        return new ConfiguredWatchMemory
        {
            Branch = previous.Branch,
            FileMask = previous.FileMask,
            SourceFilePath = previous.SourceFilePath,
            ActiveSinceUtc = previous.ActiveSinceUtc,
            LastCheckedUtc = previous.LastCheckedUtc,
            LastCompletedScanUtc = previous.LastCompletedScanUtc,
            RecentEvents = new Dictionary<long, DateTimeOffset>(previous.RecentEvents)
        };
    }

    private static DateTimeOffset ScanLowerBound(ConfiguredWatchMemory watch)
    {
        var overlapStart = watch.LastCompletedScanUtc.GetValueOrDefault(watch.ActiveSinceUtc) - Overlap;
        return overlapStart > watch.ActiveSinceUtc ? overlapStart : watch.ActiveSinceUtc;
    }

    private static async Task<(List<PushPollingOutput> Pushes, Dictionary<long, DateTimeOffset> CompletedEvents)>
        GetMatchingPushes(ConfiguredRepositoryScan scan, ConfiguredWatchMemory watch, DateTimeOffset upper)
    {
        if (watch.ActiveSinceUtc >= upper)
            return ([], []);

        var pushes = new List<PushPollingOutput>();
        var completedEvents = new Dictionary<long, DateTimeOffset>();
        var lower = ScanLowerBound(watch);
        foreach (var push in await scan.GetPushEvents())
        {
            if (push.PushData?.Ref != watch.Branch || push.CreatedAt!.Value < lower ||
                push.CreatedAt.Value <= watch.ActiveSinceUtc || watch.RecentEvents.ContainsKey(push.Id))
                continue;

            var matching = await scan.GetMatchingCommits(push, watch.SourceFilePath);
            completedEvents.Add(push.Id, push.CreatedAt.Value);
            if (matching.Count == 0)
                continue;

            pushes.Add(new PushPollingOutput
            {
                RepositoryId = push.ProjectId,
                RepositoryNameWithNamespace = scan.RepositoryName,
                BranchName = watch.Branch,
                PushTime = push.CreatedAt.Value.UtcDateTime,
                PusherUserId = push.AuthorId!.Value,
                FilePathsMatched = [watch.SourceFilePath],
                Commits = matching
            });
        }
        return (pushes, completedEvents);
    }

    private void Warn(string warning) => InvocationContext.Logger?.LogWarning("{0}", [warning]);
}
