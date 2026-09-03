using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using GitLabApiClient.Models.Projects.Responses;
using RestSharp;

namespace Apps.Gitlab.DataSourceHandlers;

public class RepositoryDataHandler : BaseInvocable, IAsyncDataSourceHandler
{
    private IEnumerable<AuthenticationCredentialsProvider> Creds =>
        InvocationContext.AuthenticationCredentialsProviders;

    public RepositoryDataHandler(InvocationContext invocationContext) : base(invocationContext)
    {
    }

    public async Task<Dictionary<string, string>> GetDataAsync(
        DataSourceContext context,
        CancellationToken cancellationToken)
    {
        var client = new BlackbirdGitlabClient(Creds);
        var request = client.CreateRequest("/projects", Method.Get);
        request.AddQueryParameter("membership", "true");
        request.AddQueryParameter("simple", "true");
        if (!string.IsNullOrWhiteSpace(context.SearchString))
            request.AddQueryParameter("search", context.SearchString);

        var content = await client.ExecutePaginatedWithErrorHandling<Project>(request, cancellationToken);
        return content
            .OrderBy(x => x.PathWithNamespace, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Id.ToString(), x => x.PathWithNamespace);
    }
}
