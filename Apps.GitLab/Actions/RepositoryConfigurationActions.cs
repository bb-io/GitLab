using System.Text.Json;
using Apps.Gitlab;
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

    [Action("Get repository configuration", Description = "Get a single configuration matching the supplied search fields")]
    public async Task<RepositoryConfigurationOutput> Get(
        [ActionParameter] ConfigurationRepositoryInput repository,
        [ActionParameter] ConfigurationBranchInput branch,
        [ActionParameter] ConfigurationPathInput file,
        [ActionParameter] GetRepositoryConfigurationRequest input)
    {
        var repositoryId = RepositoryConfigurationReader.ParseRepositoryId(repository.RepositoryId);
        var targetId = ParseOptionalRepositoryId(input.SearchRepositoryId);
        var reader = CreateReader();
        var entries = await reader.Read(repositoryId, branch.Name, file.FilePath);
        var matches = entries.Where(entry => Matches(entry, targetId, input))
            .Take(2).ToArray();
        return matches.Length switch
        {
            0 => throw new PluginMisconfigurationException("No configuration matches the supplied search fields."),
            1 => await reader.ToOutput(matches[0]),
            _ => throw new PluginMisconfigurationException(
                "Multiple configurations match. Supply more specific search filters to select one configuration.")
        };
    }

    [Action("Search repository configurations", Description = "Find configurations matching the selected filters")]
    public async Task<RepositoryConfigurationsOutput> Search(
        [ActionParameter] ConfigurationRepositoryInput repository,
        [ActionParameter] ConfigurationBranchInput branch,
        [ActionParameter] ConfigurationPathInput file,
        [ActionParameter] SearchRepositoryConfigurationsRequest input)
    {
        var targetRepository = ParseOptionalRepositoryId(input.SearchRepositoryId);
        var reader = CreateReader();
        var entries = await reader.Read(
            RepositoryConfigurationReader.ParseRepositoryId(repository.RepositoryId), branch.Name, file.FilePath);
        var matches = entries.Where(entry => Matches(entry, targetRepository, input));

        var output = new RepositoryConfigurationsOutput();
        foreach (var match in matches)
            output.Configurations.Add(await reader.ToOutput(match));
        return output;
    }

    private static int? ParseOptionalRepositoryId(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : RepositoryConfigurationReader.ParseRepositoryId(value);

    private static bool Matches(RepositoryConfiguration entry, int? repositoryId, SearchRepositoryConfigurationsRequest filters)
        => (repositoryId is null || entry.Key.RepositoryId == repositoryId) &&
           (string.IsNullOrWhiteSpace(filters.Branch) || entry.Key.Branch == filters.Branch) &&
           (string.IsNullOrWhiteSpace(filters.FileMask) || entry.Key.FileMask == filters.FileMask) &&
           (string.IsNullOrWhiteSpace(filters.SourceLocale) || entry.SourceLocale == filters.SourceLocale) &&
           (string.IsNullOrWhiteSpace(filters.ContainsTargetLocale) || entry.TargetLocales.Contains(filters.ContainsTargetLocale)) &&
           (string.IsNullOrWhiteSpace(filters.ContainsMetadata) || ContainsText(entry.Metadata, filters.ContainsMetadata));

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
