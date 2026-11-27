using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Apps.Gitlab;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using RestSharp;

namespace Apps.GitLab.Configuration;

internal sealed class RepositoryConfigurationReader(
    BlackbirdGitlabClient client,
    IFileManagementClient fileManagement,
    Action<string> warn)
{
    private const string LocalePlaceholder = "[LOCALE]";

    internal async Task<List<RepositoryConfiguration>> Read(
        int repositoryId, string branch, string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(branch))
            throw new PluginMisconfigurationException("Specify the configuration file branch.");
        if (!IsRepositoryPath(filePath))
            throw new PluginMisconfigurationException("Specify a repository-relative configuration file path.");

        var request = client.CreateRequest(
            $"/projects/{repositoryId}/repository/files/{Uri.EscapeDataString(filePath)}/raw", Method.Get);
        request.AddQueryParameter("ref", branch);
        var response = await client.ExecuteWithErrorHandling(request, cancellationToken,
            HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
        if (!response.IsSuccessStatusCode)
            throw new PluginMisconfigurationException(
                $"Configuration file '{filePath}' on branch '{branch}' of repository {repositoryId} was not found or is inaccessible.");

        try
        {
            var bytes = response.RawBytes.AsMemory();
            if (bytes.Span.StartsWith(Encoding.UTF8.Preamble))
                bytes = bytes[Encoding.UTF8.Preamble.Length..];
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = int.MaxValue });
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new PluginMisconfigurationException("The configuration file must be a JSON array.");
            return ParseEntries(document.RootElement);
        }
        catch (JsonException exception)
        {
            throw new PluginMisconfigurationException(
                $"The configuration file must be a valid JSON array. Invalid JSON at line {exception.LineNumber + 1}, byte {exception.BytePositionInLine + 1}.");
        }
    }

    private List<RepositoryConfiguration> ParseEntries(JsonElement array)
    {
        var entries = new List<RepositoryConfiguration>();
        var keyEntries = new Dictionary<ConfigurationKey, List<int>>();
        var entryNumber = 0;
        foreach (var entry in array.EnumerateArray())
        {
            entryNumber++;
            if (entry.ValueKind != JsonValueKind.Object)
            {
                WarnInvalid(entryNumber, "Entry must be a JSON object.");
                continue;
            }

            if (!TryReadKey(entry, out var key, out var error))
            {
                WarnInvalid(entryNumber, error);
                continue;
            }

            if (!keyEntries.TryGetValue(key, out var numbers))
                keyEntries.Add(key, numbers = []);
            numbers.Add(entryNumber);

            if (TryReadEntry(entry, key, entryNumber, out var configuration, out error))
                entries.Add(configuration!);
            else
                WarnInvalid(entryNumber, error);
        }

        var duplicateKeys = keyEntries.Where(pair => pair.Value.Count > 1).ToDictionary();
        foreach (var (key, numbers) in duplicateKeys)
            warn($"Skipping duplicate configuration entries {string.Join(", ", numbers)} for repository {key.RepositoryId}, branch '{key.Branch}', file mask '{key.FileMask}'.");

        return entries.Where(entry => !duplicateKeys.ContainsKey(entry.Key)).ToList();
    }

    private void WarnInvalid(int entryNumber, string error)
        => warn($"Skipping configuration entry {entryNumber}. {error}");

    private static bool TryReadKey(JsonElement entry, out ConfigurationKey key, out string error)
    {
        key = default;
        var repository = Get(entry, "repositoryId");
        int repositoryId;
        if (!(repository.ValueKind == JsonValueKind.Number && repository.TryGetInt32(out repositoryId) && repositoryId > 0) &&
            !(repository.ValueKind == JsonValueKind.String && TryRepositoryId(repository.GetString(), out repositoryId)))
        {
            error = "'repositoryId' must be a positive GitLab project ID.";
            return false;
        }

        if (!TryReadText(Get(entry, "branch"), out var branch))
        {
            error = "'branch' must be nonblank text.";
            return false;
        }

        var fileMask = ReadText(Get(entry, "fileMask"));
        if (!IsRepositoryPath(fileMask) || !fileMask.Contains(LocalePlaceholder, StringComparison.Ordinal))
        {
            error = "'fileMask' must be a repository-relative path with a [LOCALE] placeholder.";
            return false;
        }

        key = new ConfigurationKey(repositoryId, branch, fileMask);
        error = string.Empty;
        return true;
    }

    private static bool TryReadEntry(JsonElement entry, ConfigurationKey key, int entryNumber,
        out RepositoryConfiguration? configuration, out string error)
    {
        configuration = null;
        if (!TryReadText(Get(entry, "sourceLocale"), out var sourceLocale))
        {
            error = "'sourceLocale' must be nonblank text.";
            return false;
        }

        var targets = Get(entry, "targetLocales");
        if (targets.ValueKind != JsonValueKind.Array || targets.GetArrayLength() == 0 ||
            targets.EnumerateArray().Any(target => !TryReadText(target, out _)))
        {
            error = "'targetLocales' must be a nonempty array of nonblank text values.";
            return false;
        }

        var enabled = Get(entry, "enabled");
        if (enabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            error = "'enabled' must be explicitly set to true or false.";
            return false;
        }

        var metadata = Get(entry, "metadata");
        if (metadata.ValueKind != JsonValueKind.Object)
        {
            error = "'metadata' must be a JSON object.";
            return false;
        }

        var sourcePath = key.FileMask.Replace(LocalePlaceholder, sourceLocale, StringComparison.Ordinal);
        if (!IsRepositoryPath(sourcePath))
        {
            error = "'fileMask' and 'sourceLocale' must resolve to a repository-relative file path.";
            return false;
        }

        configuration = new RepositoryConfiguration(key, entryNumber, sourceLocale,
            targets.EnumerateArray().Select(ReadText).ToList(), enabled.GetBoolean(), sourcePath, metadata.Clone());
        error = string.Empty;
        return true;
    }

    internal async Task<RepositoryConfigurationOutput> ToOutput(
        RepositoryConfiguration configuration, RepositoryConfigurationOutput? output = null)
    {
        using var metadata = new MemoryStream(Encoding.UTF8.GetBytes(configuration.Metadata.GetRawText()));
        var key = configuration.Key;
        var metadataFile = await fileManagement.UploadAsync(
            metadata, "application/json", $"metadata-{key.RepositoryId}-{configuration.EntryNumber}.json")
            ?? throw new PluginApplicationException($"Could not upload metadata for configuration entry {configuration.EntryNumber}.");

        output ??= new RepositoryConfigurationOutput();
        output.RepositoryId = key.RepositoryId;
        output.Branch = key.Branch;
        output.FileMask = key.FileMask;
        output.SourceLocale = configuration.SourceLocale;
        output.TargetLocales = configuration.TargetLocales;
        output.Enabled = configuration.Enabled;
        output.SourceFilePath = configuration.SourceFilePath;
        output.Metadata = metadataFile;
        return output;
    }

    internal static int ParseRepositoryId(string? value)
        => TryRepositoryId(value, out var id)
            ? id
            : throw new PluginMisconfigurationException("Repository ID must be a positive GitLab project ID.");

    private static bool TryRepositoryId(string? value, out int id)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;

    private static bool IsRepositoryPath(string? path)
        => !string.IsNullOrWhiteSpace(path) && !path.Contains('\0') &&
           path.Split('/').All(segment => segment is not ("" or "." or ".."));

    private static JsonElement Get(JsonElement entry, string property)
        => entry.TryGetProperty(property, out var value) ? value : default;

    private static bool TryReadText(JsonElement value, out string text)
    {
        text = ReadText(value);
        return !string.IsNullOrWhiteSpace(text);
    }

    private static string ReadText(JsonElement value)
        => value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;
}
