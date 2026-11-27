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
        [ActionParameter] GetRepositoryConfigurationRequest input)
    {
        if (string.IsNullOrWhiteSpace(input.RepositoryId) || string.IsNullOrWhiteSpace(input.FilePath))
            throw new PluginMisconfigurationException("Specify the configuration repository and path to read the configuration file.");

        var targetId = ParseOptionalRepositoryId(input.SearchRepositoryId);
        var reader = CreateReader();
        var configurationRef = string.IsNullOrWhiteSpace(input.Name) ? "HEAD" : input.Name;
        var entries = await reader.Read(
            RepositoryConfigurationReader.ParseRepositoryId(input.RepositoryId), configurationRef, input.FilePath);
        var matches = entries.Where(entry => Matches(entry.Key, targetId, input.Branch, input.FileMask))
            .Take(2).ToArray();
        return matches.Length switch
        {
            0 => throw new PluginMisconfigurationException("No configuration matches the supplied search fields."),
            1 => await reader.ToOutput(matches[0]),
            _ => throw new PluginMisconfigurationException(
                "Multiple configurations match. Narrow the search using Search repository ID, Search branch, or Search file mask.")
        };
    }

    [Action("Search repository configurations", Description = "Find configurations matching the selected filters")]
    public async Task<RepositoryConfigurationsOutput> Search(
        [ActionParameter] SearchRepositoryConfigurationsRequest input)
    {
        var targetRepository = ParseOptionalRepositoryId(input.SearchRepositoryId);
        var targets = input.TargetLocales?.ToArray() ?? [];
        var reader = CreateReader();
        var entries = await reader.Read(
            RepositoryConfigurationReader.ParseRepositoryId(input.RepositoryId), input.Name, input.FilePath);
        var matches = entries.Where(entry =>
            Matches(entry.Key, targetRepository, input.Branch, input.FileMask) &&
            (string.IsNullOrWhiteSpace(input.SourceLocale) || entry.SourceLocale == input.SourceLocale) &&
            targets.All(entry.TargetLocales.Contains) &&
            (string.IsNullOrWhiteSpace(input.ContainsMetadata) || ContainsText(entry.Metadata, input.ContainsMetadata)));

        var output = new RepositoryConfigurationsOutput();
        foreach (var match in matches)
            output.Configurations.Add(await reader.ToOutput(match));
        return output;
    }

    private static int? ParseOptionalRepositoryId(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : RepositoryConfigurationReader.ParseRepositoryId(value);

    private static bool Matches(ConfigurationKey key, int? repositoryId, string? branch, string? fileMask)
        => (repositoryId is null || key.RepositoryId == repositoryId) &&
           (string.IsNullOrWhiteSpace(branch) || key.Branch == branch) &&
           (string.IsNullOrWhiteSpace(fileMask) || key.FileMask == fileMask);

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
