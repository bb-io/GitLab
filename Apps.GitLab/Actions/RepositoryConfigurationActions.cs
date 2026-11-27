using System.Text.Json;
using Apps.Gitlab;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.GitLab.Configuration;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;

namespace Apps.GitLab.Actions;

[ActionList("Repository")]
public class RepositoryConfigurationActions(InvocationContext context, IFileManagementClient fileManagement)
    : BaseInvocable(context)
{
    internal BlackbirdGitlabClient? ClientOverride { private get; init; }

    [Action("Get repository configuration", Description = "Get a configuration by repository ID, branch, and file mask")]
    public async Task<RepositoryConfigurationOutput> Get(
        [ActionParameter] GetRepositoryRequest repository,
        [ActionParameter] GetBranchRequest branch,
        [ActionParameter] ConfigurationFileInput file,
        [ActionParameter] RepositoryConfigurationInput target)
    {
        var targetId = RepositoryConfigurationReader.ParseRepositoryId(target.RepositoryId);
        if (string.IsNullOrWhiteSpace(target.Branch) || string.IsNullOrWhiteSpace(target.FileMask))
            throw new PluginMisconfigurationException("Specify the target branch and file mask.");

        var reader = CreateReader();
        var entries = await reader.Read(
            RepositoryConfigurationReader.ParseRepositoryId(repository.RepositoryId), branch.Name, file.FilePath);
        var key = new ConfigurationKey(targetId, target.Branch, target.FileMask);
        var match = entries.Find(entry => entry.Key == key)
            ?? throw new PluginMisconfigurationException(
                $"No configuration matches repository {targetId}, branch '{target.Branch}', file mask '{target.FileMask}'.");
        return await reader.ToOutput(match);
    }

    [Action("Search repository configurations", Description = "Find configurations matching the selected filters")]
    public async Task<RepositoryConfigurationsOutput> Search(
        [ActionParameter] GetRepositoryRequest repository,
        [ActionParameter] GetBranchRequest branch,
        [ActionParameter] ConfigurationFileInput file,
        [ActionParameter] RepositoryConfigurationFilters filters)
    {
        int? targetRepository = string.IsNullOrWhiteSpace(filters.RepositoryId)
            ? null
            : RepositoryConfigurationReader.ParseRepositoryId(filters.RepositoryId);
        var targets = filters.TargetLocales?.ToArray() ?? [];
        var reader = CreateReader();
        var entries = await reader.Read(
            RepositoryConfigurationReader.ParseRepositoryId(repository.RepositoryId), branch.Name, file.FilePath);
        var matches = entries.Where(entry =>
            (targetRepository is null || entry.Key.RepositoryId == targetRepository) &&
            (string.IsNullOrWhiteSpace(filters.Branch) || entry.Key.Branch == filters.Branch) &&
            (string.IsNullOrWhiteSpace(filters.FileMask) || entry.Key.FileMask == filters.FileMask) &&
            (string.IsNullOrWhiteSpace(filters.SourceLocale) || entry.SourceLocale == filters.SourceLocale) &&
            targets.All(entry.TargetLocales.Contains) &&
            (string.IsNullOrWhiteSpace(filters.ContainsMetadata) || ContainsText(entry.Metadata, filters.ContainsMetadata)));

        var output = new RepositoryConfigurationsOutput();
        foreach (var match in matches)
            output.Configurations.Add(await reader.ToOutput(match));
        return output;
    }

    private RepositoryConfigurationReader CreateReader()
        => new(ClientOverride ?? new BlackbirdGitlabClient(InvocationContext.AuthenticationCredentialsProviders),
            fileManagement, warning => InvocationContext.Logger?.LogWarning("{0}", [warning]));

    private static bool ContainsText(JsonElement metadata, string searchText)
    {
        var pending = new Stack<JsonElement>();
        pending.Push(metadata);
        while (pending.TryPop(out var value))
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in value.EnumerateObject())
                    {
                        if (property.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                            return true;
                        pending.Push(property.Value);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in value.EnumerateArray())
                        pending.Push(item);
                    break;
                default:
                    var text = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
                    if (text.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                        return true;
                    break;
            }
        }

        return false;
    }
}
