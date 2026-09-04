using System.Net;
using Apps.GitLab.Constants;
using Apps.GitLab.Dtos;
using Apps.Gitlab.Constants;
using Apps.GitLab.Models.Respository.Responses;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using Blackbird.Applications.Sdk.Utils.Extensions.String;
using Blackbird.Applications.Sdk.Utils.RestSharp;
using GitLabApiClient.Models.Projects.Responses;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using RestSharp;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Commit = GitLabApiClient.Models.Commits.Responses.Commit;

namespace Apps.Gitlab;

public class BlackbirdGitlabClient : BlackBirdRestClient
{
    private const string ApiPrefix = "/api/v4";
    private const int RetryCount = 8;
    private const int BaseBackoffSeconds = 1;
    private const int MaxBackoffSeconds = 16;
    private const int MaxRetryAfterSeconds = 60;
    private const int DefaultMaximumPages = 100;
    private static readonly TimeSpan DefaultMaximumPaginationDuration = TimeSpan.FromMinutes(5);

    private readonly IEnumerable<AuthenticationCredentialsProvider> _authenticationCredentials;
    private readonly AsyncRetryPolicy<RestResponse> _retryPolicy;

    protected override JsonSerializerSettings? JsonSettings => JsonConfig.JsonSettings;

    public string BaseUrl { get; }

    public BlackbirdGitlabClient(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders)
        : this(authenticationCredentialsProviders, null)
    {
    }

    internal BlackbirdGitlabClient(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        Func<HttpMessageHandler, HttpMessageHandler>? configureMessageHandler)
        : base(new()
        {
            BaseUrl = GetBaseUrl(authenticationCredentialsProviders).ToUri(),
            ConfigureMessageHandler = configureMessageHandler
        })
    {
        _authenticationCredentials = authenticationCredentialsProviders;
        BaseUrl = GetBaseUrl(authenticationCredentialsProviders);
        _retryPolicy = Policy
            .HandleResult<RestResponse>(response => response.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                RetryCount,
                (retryAttempt, result, _) =>
                {
                    var retryAfter = result.Result.Headers?
                        .FirstOrDefault(header => string.Equals(
                            header.Name,
                            "Retry-After",
                            StringComparison.OrdinalIgnoreCase))?
                        .Value?.ToString();

                    if (int.TryParse(retryAfter?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) &&
                        seconds >= 0)
                        return TimeSpan.FromSeconds(Math.Min(seconds, MaxRetryAfterSeconds));

                    if (DateTimeOffset.TryParse(
                            retryAfter,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal,
                            out var retryAt))
                    {
                        var serverDelay = retryAt - DateTimeOffset.UtcNow;
                        if (serverDelay <= TimeSpan.Zero)
                            return TimeSpan.Zero;
                        return serverDelay > TimeSpan.FromSeconds(MaxRetryAfterSeconds)
                            ? TimeSpan.FromSeconds(MaxRetryAfterSeconds)
                            : serverDelay;
                    }

                    var backoffSeconds = Math.Min(
                        BaseBackoffSeconds * Math.Pow(2, retryAttempt - 1),
                        MaxBackoffSeconds);
                    return TimeSpan.FromSeconds(backoffSeconds) +
                           TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
                },
                (_, _, _, _) => Task.CompletedTask);
    }

    public override Task<RestResponse> ExecuteWithErrorHandling(RestRequest request)
        => ExecuteWithErrorHandling(request, CancellationToken.None, []);

    public Task<RestResponse> ExecuteWithErrorHandling(
        RestRequest request,
        params HttpStatusCode[] allowedStatuses)
        => ExecuteWithErrorHandling(request, CancellationToken.None, allowedStatuses);

    public async Task<RestResponse> ExecuteWithErrorHandling(
        RestRequest request,
        CancellationToken cancellationToken,
        params HttpStatusCode[] allowedStatuses)
    {
        var response = await _retryPolicy.ExecuteAsync(
            token => ExecuteAsync(request, token),
            cancellationToken);
        if (response.IsSuccessStatusCode || allowedStatuses.Contains(response.StatusCode))
            return response;

        throw ConfigureErrorException(response);
    }

    public async Task<List<T>> ExecutePaginatedWithErrorHandling<T>(
        RestRequest request,
        CancellationToken cancellationToken = default,
        int maximumPages = DefaultMaximumPages,
        TimeSpan? maximumDuration = null)
    {
        if (maximumPages <= 0)
            throw new PluginApplicationException(
                $"{nameof(maximumPages)} must be greater than zero; received {maximumPages}.");

        var items = new List<T>();
        var visitedNextLinks = new HashSet<string>(StringComparer.Ordinal);
        var stopwatch = Stopwatch.StartNew();
        var durationLimit = maximumDuration ?? DefaultMaximumPaginationDuration;
        var timeLimitMessage =
            $"GitLab pagination exceeded the safety time limit of {durationLimit.TotalMinutes:g} minutes.";
        if (durationLimit <= TimeSpan.Zero)
            throw new PluginApplicationException(timeLimitMessage);

        using var paginationDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        paginationDeadline.CancelAfter(durationLimit);
        var currentRequest = request;

        for (var page = 1; ; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (page > maximumPages)
                throw new PluginApplicationException(
                    $"GitLab pagination exceeded the safety limit of {maximumPages} pages.");
            if (stopwatch.Elapsed > durationLimit)
                throw new PluginApplicationException(timeLimitMessage);

            RestResponse response;
            try
            {
                response = await ExecuteWithErrorHandling(currentRequest, paginationDeadline.Token);
            }
            catch (OperationCanceledException) when (
                !cancellationToken.IsCancellationRequested && paginationDeadline.IsCancellationRequested)
            {
                throw new PluginApplicationException(timeLimitMessage);
            }
            if (string.IsNullOrWhiteSpace(response.Content))
                throw new PluginApplicationException("GitLab returned an empty paginated response.");
            List<T>? pageItems;
            try
            {
                pageItems = JsonConvert.DeserializeObject<List<T>>(response.Content, JsonSettings);
            }
            catch (JsonException exception)
            {
                throw new PluginApplicationException(
                    $"GitLab returned invalid JSON for a paginated response: {exception.Message}");
            }
            if (pageItems is null)
                throw new PluginApplicationException("GitLab returned a null paginated response.");
            items.AddRange(pageItems);
            if (stopwatch.Elapsed > durationLimit)
                throw new PluginApplicationException(timeLimitMessage);

            var linkHeader = string.Join(",", response.Headers?
                .Where(header => string.Equals(header.Name, "Link", StringComparison.OrdinalIgnoreCase))
                .Select(header => header.Value?.ToString()) ?? []);
            string? nextLink = null;
            foreach (Match linkMatch in Regex.Matches(
                         linkHeader,
                         @"<(?<url>[^>]+)>(?<parameters>(?:\s*;\s*[^,]+)*)",
                         RegexOptions.CultureInvariant,
                         TimeSpan.FromMilliseconds(250)))
            {
                var relationMatch = Regex.Match(
                    linkMatch.Groups["parameters"].Value,
                    @"(?:^|;)\s*rel\s*=\s*\""?(?<relations>[^\"";,]+)\""?",
                    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
                    TimeSpan.FromMilliseconds(250));
                if (relationMatch.Success && relationMatch.Groups["relations"].Value
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Contains("next", StringComparer.OrdinalIgnoreCase))
                {
                    nextLink = linkMatch.Groups["url"].Value;
                    break;
                }
            }

            if (nextLink is null)
                return items;

            if (!visitedNextLinks.Add(nextLink))
                throw new PluginApplicationException("GitLab pagination returned a repeated next-page link.");

            var nextUri = Uri.TryCreate(nextLink, UriKind.Absolute, out var absoluteNextUri)
                ? absoluteNextUri
                : new Uri(response.ResponseUri ?? this.BuildUri(currentRequest), nextLink);
            var baseUri = new Uri(BaseUrl);
            if (!string.Equals(nextUri.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(nextUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase) ||
                nextUri.Port != baseUri.Port)
                throw new PluginApplicationException("GitLab pagination returned a next-page link for another host.");

            currentRequest = CreateRequest(nextUri.PathAndQuery, Method.Get);
        }
    }

    public static string GetBaseUrl(IEnumerable<AuthenticationCredentialsProvider> creds)
    {
        var connectionType = creds.Get(CredNames.ConnectionType).Value;

        return connectionType switch
        {
            ConnectionTypes.OAuth => "https://gitlab.com",
            ConnectionTypes.OAuthSelfManaged => creds.Get(CredNames.BaseUrl).Value.TrimEnd('/'),
            ConnectionTypes.PersonalAccessToken => "https://gitlab.com",
            _ => throw new Exception($"Unsupported connection type: {connectionType}")
        };
    }

    public GitLabRequest CreateRequest(string resource, Method method)
        => new(NormalizeApiResource(resource), method, _authenticationCredentials);

    public async Task<byte[]> ExecuteForBytesWithErrorHandling(RestRequest request)
    {
        var response = await ExecuteWithErrorHandling(request);

        return response.RawBytes ?? [];
    }

    public Task<Project> GetProject(int projectId) => GetProject(projectId, CancellationToken.None);

    internal async Task<Project> GetProject(int projectId, CancellationToken cancellationToken)
    {
        var request = CreateRequest($"/projects/{projectId}", Method.Get);
        var response = await ExecuteWithErrorHandling(request, cancellationToken);
        Project? project;
        try
        {
            project = JsonConvert.DeserializeObject<Project>(response.Content ?? string.Empty, JsonSettings);
        }
        catch (JsonException exception)
        {
            throw new PluginApplicationException(
                $"GitLab returned invalid JSON for project {projectId}: {exception.Message}");
        }

        return project
               ?? throw new PluginApplicationException($"GitLab returned an invalid project {projectId} response.");
    }

    public async Task<RepositoryFileResponse> GetFileInfo(int projectId, string filePath, string branch)
    {
        string endpoint = $"/projects/{projectId}/repository/files/{Uri.EscapeDataString(filePath)}";
        var request = CreateRequest(endpoint, Method.Get).AddQueryParameter("ref", branch);

        return await ExecuteWithErrorHandling<RepositoryFileResponse>(request);
    }

    public async Task<byte[]> GetArchive(int projectId, string? branchName)
    {
        var branchCommit = !string.IsNullOrWhiteSpace(branchName) ? $"?sha={Uri.EscapeDataString(branchName)}" : "";
        var request = CreateRequest($"/projects/{projectId}/repository/archive.zip{branchCommit}", Method.Get);

        return await ExecuteForBytesWithErrorHandling(request);
    }

    public async Task<Commit> PushChanges(int projectId, string? branchName, string commitMessage,
        string filePath, byte[]? file, string action)
    {
        var branch = string.IsNullOrWhiteSpace(branchName) ? (await GetProject(projectId)).DefaultBranch : branchName;

        var request = CreateRequest($"/projects/{projectId}/repository/commits", Method.Post);
        request.AddJsonBody(new
        {
            branch,
            commit_message = commitMessage,
            actions = new[]
            {
                new FileActionDto(action, filePath, file)
            }
        });

        return await ExecuteWithErrorHandling<Commit>(request);
    }

    protected override Exception ConfigureErrorException(RestResponse response)
    {
        var message = $"{(int)response.StatusCode}: {response.ErrorMessage ?? response.Content}";

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                new PluginMisconfigurationException(message),
            _ => new PluginApplicationException(message)
        };
    }

    private static string NormalizeApiResource(string resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
            return ApiPrefix;

        if (resource.StartsWith(ApiPrefix, StringComparison.OrdinalIgnoreCase))
            return resource;

        return $"{ApiPrefix}{resource}";
    }
}
