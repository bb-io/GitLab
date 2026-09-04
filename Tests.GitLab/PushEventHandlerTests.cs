using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Webhooks.Payloads;
using Apps.GitLab.Webhooks.Handlers;
using Tests.GitLab.Base;

namespace Tests.GitLab;

[TestClass]
public class PushEventHandlerTests
{
    [TestMethod]
    public async Task Unsubscribe_DeletesOnlyMatchingPushHookBeyondFirstPage()
    {
        const string otherPayloadUrl = "https://bridge.blackbird.io/webhooks/other";
        const string targetPayloadUrl = "https://bridge.blackbird.io/webhooks/target";
        var deletedPaths = new List<string>();
        var httpHandler = new StubHttpMessageHandler((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == "/api/v4/projects/101/hooks")
            {
                if (request.RequestUri.Query == "?page=2")
                    return Task.FromResult(GitLabTestData.Json(
                        $"[{{\"id\":22,\"url\":\"{targetPayloadUrl}\",\"push_events\":true}}]"));

                var response = GitLabTestData.Json(
                    $"[{{\"id\":11,\"url\":\"{otherPayloadUrl}\",\"push_events\":true}}]");
                return Task.FromResult(GitLabTestData.WithHeader(
                    response,
                    "Link",
                    "<https://gitlab.test/api/v4/projects/101/hooks?page=2>; rel=\"next\""));
            }

            if (request.Method == HttpMethod.Delete)
            {
                deletedPaths.Add(path);
                return Task.FromResult(GitLabTestData.Json("{}"));
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });
        var context = GitLabTestData.CreateContext();
        var handler = new PushEventHandler(
            context,
            new WebhookRepositoryInput { RepositoryId = "101" },
            new GetOptionalBranchRequest(),
            GitLabTestData.CreateClient(httpHandler));

        await handler.UnsubscribeAsync(
            context.AuthenticationCredentialsProviders,
            new Dictionary<string, string> { ["payloadUrl"] = targetPayloadUrl });

        CollectionAssert.AreEqual(
            new[] { "", "?page=2" },
            httpHandler.RequestUris
                .Where(uri => uri.AbsolutePath == "/api/v4/projects/101/hooks")
                .Select(uri => uri.Query)
                .ToArray());
        CollectionAssert.AreEqual(
            new[] { "/api/v4/projects/101/hooks/22" },
            deletedPaths);
    }
}
