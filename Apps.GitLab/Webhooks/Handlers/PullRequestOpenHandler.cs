using Apps.Gitlab.Webhooks.Payloads;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Webhooks;

namespace Apps.Gitlab.Webhooks.Handlers;

public class PullRequestOpenHandler : BaseWebhookHandler
{
    const string SubscriptionEvent = "pull_request";

    public PullRequestOpenHandler(InvocationContext invocationContext, [WebhookParameter(true)] WebhookRepositoryInput input) : base(invocationContext, input, SubscriptionEvent) { }
}