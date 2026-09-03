using Apps.Gitlab;
using Apps.GitLab.Utils;
using Apps.GitLab.Webhooks.Payloads;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Webhooks;
using RestSharp;

namespace Apps.GitLab.Webhooks.Handlers;

public class GroupPushEventHandler : BaseInvocable, IWebhookEventHandler
{
    private readonly GroupWebhookInput _input;

    public GroupPushEventHandler(InvocationContext invocationContext,
        [WebhookParameter(true)] GroupWebhookInput input) : base(invocationContext)
    {
        _input = input;
    }

    public async Task SubscribeAsync(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        Dictionary<string, string> values)
    {
        var groupIds = _input.GroupIds?.Distinct().ToList() ?? [];
        if (groupIds.Count == 0)
            throw new PluginMisconfigurationException("Select at least one group to watch.");

        var client = new BlackbirdGitlabClient(authenticationCredentialsProviders);
        var groups = new List<GitLabGroupResponse>();
        foreach (var groupId in groupIds)
        {
            var parsedGroupId = ParsingUtils.ParseIntOrThrow(groupId, "Group ID");
            try
            {
                var groupRequest = client.CreateRequest($"/groups/{parsedGroupId}", Method.Get);
                var group = await client.ExecuteWithErrorHandling<GitLabGroupResponse>(groupRequest);
                if (group.Archived || group.MarkedForDeletionOn is not null)
                    throw new PluginMisconfigurationException($"Group '{group.FullPath}' is not active.");
                groups.Add(group);
            }
            catch (Exception ex) when (ex is not PluginMisconfigurationException)
            {
                throw new PluginMisconfigurationException(
                    $"Cannot access GitLab group ID {parsedGroupId}. Confirm group exists and connection has Owner access. {ex.Message}");
            }
        }

        var effectiveGroups = groups
            .Where(candidate => !groups.Any(selected => selected.Id != candidate.Id &&
                candidate.FullPath.StartsWith($"{selected.FullPath}/", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var existingHooks = new Dictionary<int, List<WebhookResponse>>();
        foreach (var group in effectiveGroups)
        {
            try
            {
                if (client.BaseUrl.Equals("https://gitlab.com", StringComparison.OrdinalIgnoreCase))
                {
                    var namespaceRequest = client.CreateRequest($"/namespaces/{group.Id}", Method.Get);
                    var groupNamespace = await client.ExecuteWithErrorHandling<GitLabGroupResponse>(namespaceRequest);
                    if (groupNamespace.Plan?.Equals("free", StringComparison.OrdinalIgnoreCase) == true)
                        throw new PluginMisconfigurationException(
                            $"Group '{group.FullPath}' uses GitLab Free. Group webhook delivery requires GitLab Premium or Ultimate.");
                }

                var listRequest = client.CreateRequest($"/groups/{group.Id}/hooks", Method.Get);
                existingHooks[group.Id] = await client.ExecutePaginatedWithErrorHandling<WebhookResponse>(listRequest);
            }
            catch (PluginMisconfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PluginMisconfigurationException(
                    $"Cannot manage hooks for group '{group.FullPath}'. GitLab group hooks require Owner access and a supported Premium or Ultimate tier. {ex.Message}");
            }
        }

        var createdHooks = new List<(GitLabGroupResponse Group, int HookId)>();
        GitLabGroupResponse? currentGroup = null;
        try
        {
            foreach (var group in effectiveGroups)
            {
                currentGroup = group;
                if (existingHooks[group.Id].Any(x => x.Url == values["payloadUrl"]))
                    continue;

                var createRequest = client.CreateRequest($"/groups/{group.Id}/hooks", Method.Post);
                createRequest.AddJsonBody(new
                {
                    url = values["payloadUrl"],
                    push_events = true,
                    tag_push_events = false,
                    branch_filter_strategy = "all_branches",
                    enable_ssl_verification = true
                });
                var hook = await client.ExecuteWithErrorHandling<WebhookResponse>(createRequest);
                createdHooks.Add((group, hook.Id));
            }
        }
        catch (Exception ex)
        {
            var rollbackErrors = new List<string>();
            foreach (var createdHook in createdHooks)
            {
                try
                {
                    var deleteRequest = client.CreateRequest(
                        $"/groups/{createdHook.Group.Id}/hooks/{createdHook.HookId}", Method.Delete);
                    await client.ExecuteWithErrorHandling(deleteRequest);
                }
                catch (Exception rollbackException)
                {
                    rollbackErrors.Add($"{createdHook.Group.FullPath}: {rollbackException.Message}");
                }
            }

            var rollbackMessage = rollbackErrors.Count == 0
                ? "Hooks created during this attempt were rolled back."
                : $"Rollback errors: {string.Join("; ", rollbackErrors)}";
            throw new PluginApplicationException(
                $"Failed to create group hook for '{currentGroup?.FullPath ?? "unknown group"}'. Confirm Owner access and GitLab Premium or Ultimate tier. {ex.Message} {rollbackMessage}");
        }
    }

    public async Task UnsubscribeAsync(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        Dictionary<string, string> values)
    {
        var client = new BlackbirdGitlabClient(authenticationCredentialsProviders);
        var errors = new List<string>();
        var groups = new List<GitLabGroupResponse>();

        foreach (var groupId in _input.GroupIds?.Distinct() ?? [])
        {
            if (!int.TryParse(groupId, out var parsedGroupId))
            {
                errors.Add($"Invalid group ID '{groupId}'.");
                continue;
            }

            try
            {
                var groupRequest = client.CreateRequest($"/groups/{parsedGroupId}", Method.Get);
                groups.Add(await client.ExecuteWithErrorHandling<GitLabGroupResponse>(groupRequest));
            }
            catch (Exception ex)
            {
                errors.Add($"Group ID {parsedGroupId}: {ex.Message}");
            }
        }

        var effectiveGroups = groups
            .Where(candidate => !groups.Any(selected => selected.Id != candidate.Id &&
                candidate.FullPath.StartsWith($"{selected.FullPath}/", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var group in effectiveGroups)
        {
            List<WebhookResponse> hooks;
            try
            {
                var listRequest = client.CreateRequest($"/groups/{group.Id}/hooks", Method.Get);
                hooks = await client.ExecutePaginatedWithErrorHandling<WebhookResponse>(listRequest);
            }
            catch (Exception ex)
            {
                errors.Add($"{group.FullPath}: {ex.Message}");
                continue;
            }

            foreach (var hook in hooks.Where(x => x.Url == values["payloadUrl"]))
            {
                try
                {
                    var deleteRequest = client.CreateRequest($"/groups/{group.Id}/hooks/{hook.Id}", Method.Delete);
                    await client.ExecuteWithErrorHandling(deleteRequest);
                }
                catch (Exception ex)
                {
                    errors.Add($"{group.FullPath} hook {hook.Id}: {ex.Message}");
                }
            }
        }

        if (errors.Count > 0)
            throw new PluginApplicationException($"Some GitLab group hooks could not be removed: {string.Join("; ", errors)}");
    }
}
