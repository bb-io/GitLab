using System.Diagnostics;
using System.Net;
using Apps.GitLab.Polling.Models;
using Blackbird.Applications.Sdk.Common.Exceptions;
using RestSharp;
using Tests.GitLab.Base;

namespace Tests.GitLab;

[TestClass]
public class BlackbirdGitlabClientTests
{
    [TestMethod]
    public async Task Pagination_FollowsLinkAcrossShortAndEmptyPages()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            var query = request.RequestUri!.Query;
            var response = query switch
            {
                "" => GitLabTestData.JsonFixture("events/page-one.json"),
                "?cursor=empty" => GitLabTestData.JsonFixture("events/empty.json"),
                "?cursor=tail" => GitLabTestData.JsonFixture("events/page-three.json"),
                _ => throw new InvalidOperationException($"Unexpected request: {request.RequestUri}")
            };

            if (query == "")
                GitLabTestData.WithHeader(response, "Link",
                    "<https://gitlab.test/api/v4/events?cursor=empty>; rel=\"next\"");
            else if (query == "?cursor=empty")
                GitLabTestData.WithHeader(response, "Link",
                    "<https://gitlab.test/api/v4/events?cursor=tail>; rel=\"next\"");

            return Task.FromResult(response);
        });
        var client = GitLabTestData.CreateClient(handler);

        var events = await client.ExecutePaginatedWithErrorHandling<GitLabPushEvent>(
            client.CreateRequest("/events", Method.Get));

        CollectionAssert.AreEqual(new long[] { 9001, 9003 }, events.Select(x => x.Id).ToArray());
        CollectionAssert.AreEqual(
            new[] { "", "?cursor=empty", "?cursor=tail" },
            handler.RequestUris.Select(x => x.Query).ToArray());
    }

    [TestMethod]
    public async Task Pagination_ResolvesRelativeLinkAndFindsNextRelationAfterOtherParameters()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            var query = request.RequestUri!.Query;
            var response = query switch
            {
                "" => GitLabTestData.JsonFixture("events/page-one.json"),
                "?cursor=tail" => GitLabTestData.JsonFixture("events/page-three.json"),
                _ => throw new InvalidOperationException($"Unexpected request: {request.RequestUri}")
            };

            if (query == "")
                GitLabTestData.WithHeader(response, "Link",
                    "<?cursor=tail>; type=\"application/json\"; rel=\"prev next\"");

            return Task.FromResult(response);
        });
        var client = GitLabTestData.CreateClient(handler);

        var events = await client.ExecutePaginatedWithErrorHandling<GitLabPushEvent>(
            client.CreateRequest("/events", Method.Get));

        CollectionAssert.AreEqual(new long[] { 9001, 9003 }, events.Select(x => x.Id).ToArray());
        CollectionAssert.AreEqual(
            new[] { "", "?cursor=tail" },
            handler.RequestUris.Select(x => x.Query).ToArray());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("null")]
    public async Task Pagination_EmptyOrNullPayloadThrows(string payload)
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(GitLabTestData.Json(payload)));
        var client = GitLabTestData.CreateClient(handler);

        await Assert.ThrowsExactlyAsync<PluginApplicationException>(() =>
            client.ExecutePaginatedWithErrorHandling<GitLabPushEvent>(
                client.CreateRequest("/events", Method.Get)));
    }

    [TestMethod]
    public async Task Pagination_MalformedJsonThrowsPluginApplicationException()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(GitLabTestData.Json("{")));
        var client = GitLabTestData.CreateClient(handler);

        var exception = await Assert.ThrowsExactlyAsync<PluginApplicationException>(() =>
            client.ExecutePaginatedWithErrorHandling<GitLabPushEvent>(
                client.CreateRequest("/events", Method.Get)));

        StringAssert.Contains(exception.Message, "invalid JSON");
    }

    [TestMethod]
    public async Task GetProject_MalformedJsonThrowsPluginApplicationException()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(GitLabTestData.Json("{")));
        var client = GitLabTestData.CreateClient(handler);

        var exception = await Assert.ThrowsExactlyAsync<PluginApplicationException>(() =>
            client.GetProject(101));

        StringAssert.Contains(exception.Message, "invalid JSON for project 101");
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public async Task Pagination_NonPositiveMaximumPagesThrowsWithoutRequest(int maximumPages)
    {
        var calls = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(GitLabTestData.Json("[]"));
        });
        var client = GitLabTestData.CreateClient(handler);

        var exception = await Assert.ThrowsExactlyAsync<PluginApplicationException>(() =>
            client.ExecutePaginatedWithErrorHandling<GitLabPushEvent>(
                client.CreateRequest("/events", Method.Get), maximumPages: maximumPages));

        StringAssert.Contains(exception.Message, nameof(maximumPages));
        StringAssert.Contains(exception.Message, maximumPages.ToString());
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public async Task Pagination_TimeLimitCancelsRetryAfterWait()
    {
        var calls = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            Interlocked.Increment(ref calls);
            var response = GitLabTestData.Json("{\"message\":\"rate limited\"}",
                HttpStatusCode.TooManyRequests);
            return Task.FromResult(GitLabTestData.WithHeader(response, "Retry-After", "120"));
        });
        var client = GitLabTestData.CreateClient(handler);
        var stopwatch = Stopwatch.StartNew();

        var exception = await Assert.ThrowsExactlyAsync<PluginApplicationException>(() =>
            client.ExecutePaginatedWithErrorHandling<GitLabPushEvent>(
                client.CreateRequest("/events", Method.Get),
                maximumDuration: TimeSpan.FromMilliseconds(100)));

        StringAssert.Contains(exception.Message, "safety time limit");
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task Pagination_SafetyCapThrowsWithoutFetchingAnotherPage()
    {
        var calls = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var page = Interlocked.Increment(ref calls);
            var response = GitLabTestData.Json("[]");
            GitLabTestData.WithHeader(response, "Link",
                $"<https://gitlab.test/api/v4/events?page={page + 1}>; rel=\"next\"");
            return Task.FromResult(response);
        });
        var client = GitLabTestData.CreateClient(handler);

        var exception = await Assert.ThrowsExactlyAsync<PluginApplicationException>(() =>
            client.ExecutePaginatedWithErrorHandling<GitLabPushEvent>(
                client.CreateRequest("/events", Method.Get), maximumPages: 2));

        StringAssert.Contains(exception.Message, "safety limit of 2 pages");
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public async Task RateLimit_ZeroRetryAfterRetriesAndSucceeds()
    {
        var calls = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                var throttled = GitLabTestData.Json("{\"message\":\"rate limited\"}",
                    HttpStatusCode.TooManyRequests);
                return Task.FromResult(GitLabTestData.WithHeader(throttled, "Retry-After", "0"));
            }

            return Task.FromResult(GitLabTestData.JsonFixture("events/page-one.json"));
        });
        var client = GitLabTestData.CreateClient(handler);

        var events = await client.ExecuteWithErrorHandling<List<GitLabPushEvent>>(
            client.CreateRequest("/events", Method.Get));

        Assert.AreEqual(2, calls);
        Assert.AreEqual(9001, events.Single().Id);
    }

    [TestMethod]
    public async Task RateLimit_RetryAfterWaitIsCancelledByCallerDeadline()
    {
        var calls = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            Interlocked.Increment(ref calls);
            var response = GitLabTestData.Json("{\"message\":\"rate limited\"}",
                HttpStatusCode.TooManyRequests);
            return Task.FromResult(GitLabTestData.WithHeader(response, "Retry-After", "120"));
        });
        var client = GitLabTestData.CreateClient(handler);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await client.ExecuteWithErrorHandling(
                client.CreateRequest("/events", Method.Get), cancellation.Token);
            Assert.Fail("Expected retry wait cancellation.");
        }
        catch (OperationCanceledException)
        {
        }

        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task RateLimit_ExhaustionSurfacesTerminal429()
    {
        var calls = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            Interlocked.Increment(ref calls);
            var response = GitLabTestData.Json("{\"message\":\"still limited\"}",
                HttpStatusCode.TooManyRequests);
            return Task.FromResult(GitLabTestData.WithHeader(response, "Retry-After", "0"));
        });
        var client = GitLabTestData.CreateClient(handler);

        var exception = await Assert.ThrowsExactlyAsync<PluginApplicationException>(() =>
            client.ExecuteWithErrorHandling(client.CreateRequest("/events", Method.Get)));

        Assert.AreEqual(9, calls);
        StringAssert.Contains(exception.Message, "429");
    }

    [TestMethod]
    public async Task Non429Failure_IsNotRetried()
    {
        var calls = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(GitLabTestData.Json("{\"message\":\"unavailable\"}",
                HttpStatusCode.ServiceUnavailable));
        });
        var client = GitLabTestData.CreateClient(handler);

        await Assert.ThrowsExactlyAsync<PluginApplicationException>(() =>
            client.ExecuteWithErrorHandling(client.CreateRequest("/events", Method.Get)));

        Assert.AreEqual(1, calls);
    }
}
