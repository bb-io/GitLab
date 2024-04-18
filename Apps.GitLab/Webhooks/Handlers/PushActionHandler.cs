using Apps.Gitlab.Webhooks.Payloads;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Webhooks;

namespace Apps.Gitlab.Webhooks.Handlers;

public class PushActionHandler : BaseWebhookHandler
{
    const string SubscriptionEvent = "push";

    public PushActionHandler(InvocationContext invocationContext, [WebhookParameter(true)] WebhookRepositoryInput input) : base(invocationContext, input, SubscriptionEvent) { }
}