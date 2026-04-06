using Apps.Gitlab;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Webhooks.Payloads;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Webhooks;
using Apps.GitLab.Utils;
using Newtonsoft.Json;
using RestSharp;
using Apps.GitLab.Webhooks.Payloads;

namespace Apps.GitLab.Webhooks.Handlers;

public class PushEventHandler : BaseInvocable, IWebhookEventHandler
{
    private int RepositoryId { get; set; }
    private GetOptionalBranchRequest BranchRequest { get; set; }

    public PushEventHandler(InvocationContext invocationContext,
        [WebhookParameter(true)] WebhookRepositoryInput repositoryRequest,
        [WebhookParameter(true)] GetOptionalBranchRequest branchRequest) : base(invocationContext)
    {
        RepositoryId = ParsingUtils.ParseIntOrThrow(repositoryRequest.RepositoryId, "Repository ID");
        BranchRequest = branchRequest;
    }

    public async Task SubscribeAsync(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        Dictionary<string, string> values)
    {
        var client = new BlackbirdGitlabClient(authenticationCredentialsProviders);
        var request = client.CreateRequest($"/projects/{RepositoryId}/hooks", Method.Post);
        request.AddJsonBody(new CreateWebhookRequest
        {
            Url = values["payloadUrl"],
            PushEvents = true,
            PushEventsBranchFilter = !string.IsNullOrEmpty(BranchRequest?.Name) ? BranchRequest.Name : null
        });

        await client.ExecuteWithErrorHandling(request);
    }

    public async Task UnsubscribeAsync(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        Dictionary<string, string> values)
    {
        var client = new BlackbirdGitlabClient(authenticationCredentialsProviders);
        var listRequest = client.CreateRequest($"/projects/{RepositoryId}/hooks", Method.Get);
        var projectWebhooks = await client.ExecuteWithErrorHandling<List<WebhookResponse>>(listRequest);
        var webhook = projectWebhooks.FirstOrDefault(x => x.PushEvents);

        if (webhook != null)
        {
            var deleteRequest = client.CreateRequest($"/projects/{RepositoryId}/hooks/{webhook.Id}", Method.Delete);
            await client.ExecuteWithErrorHandling(deleteRequest);
        }
    }
}
