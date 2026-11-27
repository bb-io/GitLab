using System.Globalization;
using System.Net;
using Apps.Gitlab;
using Apps.Gitlab.Constants;
using Apps.GitLab.Polling.Models;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Apps.GitLab.Polling;

internal sealed class ConfiguredRepositoryScan(
    BlackbirdGitlabClient client,
    int projectId,
    DateTimeOffset upper,
    DateTimeOffset lower,
    Action<string> warn,
    CancellationToken cancellationToken)
{
    private readonly Dictionary<string, bool> _branches = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Branch, string Path), bool> _sources = [];
    private readonly Dictionary<long, List<GitLabCompareCommit>> _pushCommits = [];
    private readonly Dictionary<string, HashSet<string>> _modifiedPaths = new(StringComparer.Ordinal);
    private List<GitLabPushEvent>? _events;

    internal string RepositoryName { get; private set; } = string.Empty;

    private string ProjectResource => $"/projects/{projectId}";

    internal async Task<bool> CheckRepository()
    {
        var response = await client.ExecuteWithErrorHandling(client.CreateRequest(ProjectResource, Method.Get),
            cancellationToken, HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
        if (!response.IsSuccessStatusCode)
        {
            warn(response.StatusCode == HttpStatusCode.Forbidden
                ? $"No read permission for repository {projectId}."
                : $"Repository {projectId} was not found or is inaccessible.");
            return false;
        }

        RepositoryName = JObject.Parse(response.Content ?? string.Empty).Value<string>("path_with_namespace")
            ?? throw new PluginApplicationException($"GitLab project {projectId} response omitted path_with_namespace.");
        return true;
    }

    internal async Task<bool> CheckSource(string branch, string path)
    {
        if (!_branches.TryGetValue(branch, out var branchExists))
        {
            var branchRequest = client.CreateRequest(
                $"{ProjectResource}/repository/branches/{Uri.EscapeDataString(branch)}", Method.Get);
            branchExists = await ResourceExists(branchRequest);
            _branches.Add(branch, branchExists);
            if (!branchExists)
                warn($"Branch '{branch}' does not exist in repository {projectId}.");
        }
        if (!branchExists)
            return false;

        if (!_sources.TryGetValue((branch, path), out var fileExists))
        {
            var fileRequest = client.CreateRequest(
                $"{ProjectResource}/repository/files/{Uri.EscapeDataString(path)}", Method.Head);
            fileRequest.AddQueryParameter("ref", branch);
            fileExists = await ResourceExists(fileRequest);
            _sources.Add((branch, path), fileExists);
            if (!fileExists)
                warn($"Source file '{path}' does not exist on branch '{branch}' in repository {projectId}.");
        }
        return fileExists;
    }

    private async Task<bool> ResourceExists(RestRequest request)
    {
        var response = await client.ExecuteWithErrorHandling(request, cancellationToken,
            HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new ConfiguredRepositoryUnavailableException();
        return response.IsSuccessStatusCode;
    }

    internal async Task<List<GitLabPushEvent>> GetPushEvents()
    {
        if (_events is not null)
            return _events;

        var request = client.CreateRequest($"{ProjectResource}/events", Method.Get)
            .AddQueryParameter("action", "pushed")
            .AddQueryParameter("after", lower.UtcDateTime.Date.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .AddQueryParameter("before", upper.UtcDateTime.Date.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .AddQueryParameter("sort", "desc")
            .AddQueryParameter("per_page", 100);

        var events = await client.ExecutePaginatedAllowingUnavailable<GitLabPushEvent>(request, cancellationToken, 200)
            ?? throw new ConfiguredRepositoryUnavailableException();
        _events = [];
        foreach (var push in events.Where(push => push.ProjectId == projectId))
        {
            if (push.Id <= 0 || push.CreatedAt is null)
                throw new PluginApplicationException($"GitLab returned an incomplete event for repository {projectId}.");
            if (push.CreatedAt < lower || push.CreatedAt > upper)
                continue;
            if (IsBranchPush(push))
                _events.Add(push);
            else if (push.PushData?.RefType != "tag")
                warn($"Skipping GitLab push event {push.Id} for repository {projectId} because it lacks a usable branch and commit range.");
        }

        _events = _events.DistinctBy(push => push.Id)
            .OrderByDescending(push => push.CreatedAt).ThenByDescending(push => push.Id).ToList();
        return _events;
    }

    private static bool IsBranchPush(GitLabPushEvent push)
        => push.AuthorId.HasValue &&
           push.PushData is { CommitCount: > 0, Action: "pushed", RefType: "branch" } data &&
           !string.IsNullOrWhiteSpace(data.Ref) && IsCommitId(data.CommitFrom) && IsCommitId(data.CommitTo);

    private static bool IsCommitId(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Any(character => character != '0');

    internal async Task<List<PushCommitOutput>> GetMatchingCommits(GitLabPushEvent push, string sourcePath)
    {
        if (!_pushCommits.TryGetValue(push.Id, out var commits))
        {
            commits = await GetPushCommits(push);
            _pushCommits.Add(push.Id, commits);
        }

        var matches = new List<PushCommitOutput>();
        foreach (var commit in commits)
        {
            if (!_modifiedPaths.TryGetValue(commit.Id, out var paths))
            {
                paths = await GetModifiedPaths(commit.Id);
                _modifiedPaths.Add(commit.Id, paths);
            }

            if (paths.Contains(sourcePath))
                matches.Add(new PushCommitOutput
                {
                    Hash = commit.Id,
                    Message = commit.Message ?? string.Empty,
                    AuthorName = commit.AuthorName ?? string.Empty,
                    AuthorEmail = commit.AuthorEmail ?? string.Empty
                });
        }
        return matches;
    }

    private async Task<List<GitLabCompareCommit>> GetPushCommits(GitLabPushEvent push)
    {
        var request = client.CreateRequest($"{ProjectResource}/repository/compare", Method.Get)
            .AddQueryParameter("from", push.PushData!.CommitFrom!)
            .AddQueryParameter("to", push.PushData.CommitTo!)
            .AddQueryParameter("straight", "true");
        var response = await client.ExecuteWithErrorHandling(request, cancellationToken,
            HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new ConfiguredRepositoryUnavailableException();
        if (!response.IsSuccessStatusCode)
        {
            if (response.Content?.Contains("Commit Not Found", StringComparison.OrdinalIgnoreCase) == true)
            {
                warn($"Skipping GitLab push event {push.Id} for repository {projectId} because its commit range is unavailable.");
                return [];
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new ConfiguredRepositoryUnavailableException();

            throw new PluginApplicationException(
                $"Could not compare the commit range for GitLab push event {push.Id} in repository {projectId} (HTTP {(int)response.StatusCode}).");
        }

        var comparison = JsonConvert.DeserializeObject<GitLabCompareResponse>(response.Content ?? string.Empty, JsonConfig.JsonSettings);
        if (comparison?.Commits is not { } commits || commits.Any(commit => string.IsNullOrWhiteSpace(commit.Id)))
            throw new PluginApplicationException($"GitLab returned an invalid commit range for push event {push.Id} in repository {projectId}.");
        return commits.DistinctBy(commit => commit.Id).ToList();
    }

    private async Task<HashSet<string>> GetModifiedPaths(string commitId)
    {
        var request = client.CreateRequest($"{ProjectResource}/repository/commits/{Uri.EscapeDataString(commitId)}/diff", Method.Get)
            .AddQueryParameter("per_page", 100);
        var diffs = await client.ExecutePaginatedAllowingUnavailable<GitLabCommitDiff>(request, cancellationToken, 100)
            ?? throw new ConfiguredRepositoryUnavailableException();
        if (diffs.Any(diff => string.IsNullOrWhiteSpace(diff.NewPath) || diff.NewFile is null ||
                              diff.DeletedFile is null || diff.RenamedFile is null))
            throw new PluginApplicationException($"GitLab returned an incomplete diff for commit {commitId} in repository {projectId}.");

        return diffs
            .Where(diff => diff is { NewFile: false, DeletedFile: false, RenamedFile: false })
            .Select(diff => diff.NewPath)
            .ToHashSet(StringComparer.Ordinal);
    }
}

internal sealed class ConfiguredRepositoryUnavailableException : Exception;
