using Blackbird.Applications.Sdk.Common.Webhooks;
using Blackbird.Applications.Sdk.Common;
using Apps.Gitlab.Webhooks.Payloads;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using Apps.Gitlab;
using GitLabApiClient.Internal.Paths;
using Apps.Gitlab.Models.Branch.Requests;

namespace Apps.GitLab.Webhooks.Handlers;

public class PushEventHandler : BaseInvocable, IWebhookEventHandler
{
    private ProjectId RepositoryId { get; set; }
    private GetOptionalBranchRequest BranchRequest { get; set; }

    public PushEventHandler(InvocationContext invocationContext, 
        [WebhookParameter(true)] WebhookRepositoryInput repositoryRequest, 
        [WebhookParameter(true)] GetOptionalBranchRequest branchRequest) : base(invocationContext)
    {
        RepositoryId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        BranchRequest = branchRequest;
    }

    public async Task SubscribeAsync(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        Dictionary<string, string> values)
    {
        var createWebhookRequest = new GitLabApiClient.Models.Webhooks.Requests.CreateWebhookRequest(values["payloadUrl"])
        {
            PushEvents = true,
        };
        if (BranchRequest != null && !string.IsNullOrEmpty(BranchRequest.Name))
        {
            createWebhookRequest.PushEventsBranchFilter = BranchRequest.Name;
        }
        await new BlackbirdGitlabClient(authenticationCredentialsProviders).Client.Webhooks.CreateAsync(RepositoryId, createWebhookRequest);
    }

    public async Task UnsubscribeAsync(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        Dictionary<string, string> values)
    {
        var projectWebhooks = await new BlackbirdGitlabClient(authenticationCredentialsProviders).Client.Webhooks.GetAsync(RepositoryId);
        var webhook = projectWebhooks.FirstOrDefault(x => x.PushEvents);
        if(webhook != null) 
        {
            await new BlackbirdGitlabClient(authenticationCredentialsProviders).Client.Webhooks.DeleteAsync(RepositoryId, webhook.Id);
        }        
    }
}