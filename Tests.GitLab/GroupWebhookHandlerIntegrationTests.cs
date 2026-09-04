using Apps.Gitlab;
using Apps.GitLab.Constants;
using Apps.GitLab.Webhooks.Handlers;
using Apps.GitLab.Webhooks.Payloads;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using RestSharp;

namespace Tests.GitLab;

[TestClass]
public class GroupWebhookHandlerIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task SubscribeTwiceAndUnsubscribe_PreservesUnrelatedHook()
    {
        var token = Environment.GetEnvironmentVariable("GITLAB_TEST_TOKEN");
        var groupId = Environment.GetEnvironmentVariable("GITLAB_GROUP_WEBHOOK_TEST_GROUP_ID");
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(groupId))
            Assert.Inconclusive("Set GITLAB_TEST_TOKEN and GITLAB_GROUP_WEBHOOK_TEST_GROUP_ID to run real lifecycle test.");

        var credentials = new List<AuthenticationCredentialsProvider>
        {
            new(CredNames.ConnectionType, ConnectionTypes.PersonalAccessToken),
            new(CredNames.Authorization, token!)
        };
        var context = new InvocationContext { AuthenticationCredentialsProviders = credentials };
        var client = new BlackbirdGitlabClient(credentials);
        var callbackUrl = $"https://example.com/blackbird/{Guid.NewGuid()}";
        var controlUrl = $"https://example.com/control/{Guid.NewGuid()}";
        var values = new Dictionary<string, string> { ["payloadUrl"] = callbackUrl };
        var handler = new GroupPushEventHandler(context,
            new GroupWebhookInput { GroupIds = [groupId!] });
        WebhookResponse? controlHook = null;

        try
        {
            var controlRequest = client.CreateRequest($"/groups/{groupId}/hooks", Method.Post);
            controlRequest.AddJsonBody(new { url = controlUrl, push_events = true });
            controlHook = await client.ExecuteWithErrorHandling<WebhookResponse>(controlRequest);

            await handler.SubscribeAsync(credentials, values);
            await handler.SubscribeAsync(credentials, values);

            var hooks = await client.ExecutePaginatedWithErrorHandling<WebhookResponse>(
                client.CreateRequest($"/groups/{groupId}/hooks", Method.Get));
            Assert.HasCount(1, hooks.Where(x => x.Url == callbackUrl));
            Assert.HasCount(1, hooks.Where(x => x.Url == controlUrl));

            await handler.UnsubscribeAsync(credentials, values);
            hooks = await client.ExecutePaginatedWithErrorHandling<WebhookResponse>(
                client.CreateRequest($"/groups/{groupId}/hooks", Method.Get));
            Assert.IsFalse(hooks.Any(x => x.Url == callbackUrl));
            Assert.IsTrue(hooks.Any(x => x.Url == controlUrl));
        }
        finally
        {
            try
            {
                await handler.UnsubscribeAsync(credentials, values);
            }
            finally
            {
                if (controlHook is not null)
                {
                    await client.ExecuteWithErrorHandling(
                        client.CreateRequest($"/groups/{groupId}/hooks/{controlHook.Id}", Method.Delete),
                        System.Net.HttpStatusCode.NotFound);
                }
            }
        }
    }
}
