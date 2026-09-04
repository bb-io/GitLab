using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using GitLabApiClient.Models.Projects.Responses;
using RestSharp;

namespace Apps.Gitlab.DataSourceHandlers;

public class RepositoryDataHandler : BaseInvocable, IAsyncDataSourceHandler
{
    private readonly BlackbirdGitlabClient? _client;

    private IEnumerable<AuthenticationCredentialsProvider> Creds =>
        InvocationContext.AuthenticationCredentialsProviders;

    public RepositoryDataHandler(InvocationContext invocationContext) : base(invocationContext)
    {
    }

    internal RepositoryDataHandler(InvocationContext invocationContext, BlackbirdGitlabClient client)
        : base(invocationContext)
    {
        _client = client;
    }

    public async Task<Dictionary<string, string>> GetDataAsync(
        DataSourceContext context,
        CancellationToken cancellationToken)
    {
        var client = _client ?? new BlackbirdGitlabClient(Creds);
        var request = client.CreateRequest("/projects", Method.Get);
        request.AddQueryParameter("membership", "true");
        request.AddQueryParameter("simple", "true");
        request.AddQueryParameter("search_namespaces", "true");
        request.AddQueryParameter("per_page", 100);
        if (!string.IsNullOrWhiteSpace(context.SearchString))
            request.AddQueryParameter("search", context.SearchString);

        var content = await client.ExecutePaginatedWithErrorHandling<Project>(request, cancellationToken);
        return content
            .OrderBy(x => x.PathWithNamespace, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Id.ToString(), x => x.PathWithNamespace);
    }
}
