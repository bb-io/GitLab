using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Apps.Gitlab;
using Apps.GitLab.Constants;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Tests.GitLab.Base;

internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
{
    private readonly ConcurrentQueue<Uri> _requestUris = new();

    public IReadOnlyCollection<Uri> RequestUris => _requestUris.ToArray();

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requestUris.Enqueue(request.RequestUri!);
        return responseFactory(request, cancellationToken);
    }
}

internal static class GitLabTestData
{
    public static readonly DateTimeOffset ScanTime = new(2026, 9, 4, 16, 0, 0, TimeSpan.Zero);

    public static InvocationContext CreateContext() => new()
    {
        AuthenticationCredentialsProviders =
        [
            new AuthenticationCredentialsProvider(CredNames.ConnectionType, ConnectionTypes.OAuthSelfManaged),
            new AuthenticationCredentialsProvider(CredNames.BaseUrl, "https://gitlab.test"),
            new AuthenticationCredentialsProvider(CredNames.Authorization, "sanitized-test-token")
        ]
    };

    public static BlackbirdGitlabClient CreateClient(StubHttpMessageHandler handler) =>
        new(
        [
            new AuthenticationCredentialsProvider(CredNames.ConnectionType, ConnectionTypes.OAuthSelfManaged),
            new AuthenticationCredentialsProvider(CredNames.BaseUrl, "https://gitlab.test"),
            new AuthenticationCredentialsProvider(CredNames.Authorization, "sanitized-test-token")
        ], _ => handler);

    public static HttpResponseMessage Json(string content, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

    public static HttpResponseMessage JsonFixture(
        string relativePath,
        HttpStatusCode statusCode = HttpStatusCode.OK)
        => Json(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", relativePath)), statusCode);

    public static HttpResponseMessage WithHeader(
        HttpResponseMessage response,
        string name,
        string value)
    {
        response.Headers.TryAddWithoutValidation(name, value);
        return response;
    }

}
