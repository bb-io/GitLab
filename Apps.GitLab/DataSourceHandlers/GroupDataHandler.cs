using Apps.GitLab.Webhooks.Payloads;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using RestSharp;

namespace Apps.Gitlab.DataSourceHandlers;

public class GroupDataHandler(InvocationContext invocationContext)
    : GitLabDataHandler(invocationContext), IAsyncDataSourceHandler
{
    public async Task<Dictionary<string, string>> GetDataAsync(DataSourceContext context,
        CancellationToken cancellationToken)
    {
        var request = RestClient.CreateRequest("/groups", Method.Get);
        request.AddQueryParameter("min_access_level", 50);
        request.AddQueryParameter("order_by", "name");
        request.AddQueryParameter("sort", "asc");
        if (!string.IsNullOrWhiteSpace(context.SearchString))
            request.AddQueryParameter("search", context.SearchString);

        var groups = await RestClient.ExecutePaginatedWithErrorHandling<GitLabGroupResponse>(request,
            cancellationToken);

        return groups
            .Where(x => !x.Archived && x.MarkedForDeletionOn is null)
            .OrderBy(x => x.ParentId.HasValue)
            .ThenBy(x => x.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Id.ToString(), x => x.FullPath);
    }
}
