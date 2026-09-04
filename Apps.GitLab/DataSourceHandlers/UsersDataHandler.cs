using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using Apps.GitLab.Models.User.Responses;
using RestSharp;

namespace Apps.Gitlab.DataSourceHandlers;

public class UsersDataHandler : BaseInvocable, IAsyncDataSourceHandler
{
    private readonly BlackbirdGitlabClient? _client;

    private IEnumerable<AuthenticationCredentialsProvider> Creds =>
        InvocationContext.AuthenticationCredentialsProviders;

    public UsersDataHandler(InvocationContext invocationContext) : base(invocationContext)
    {
    }

    internal UsersDataHandler(InvocationContext invocationContext, BlackbirdGitlabClient client)
        : base(invocationContext)
    {
        _client = client;
    }

    public async Task<Dictionary<string, string>> GetDataAsync(
        DataSourceContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.SearchString))
            return new Dictionary<string, string>();

        var client = _client ?? new BlackbirdGitlabClient(Creds);
        var request = client.CreateRequest("/users", Method.Get);
        request.AddQueryParameter("search", context.SearchString);
        request.AddQueryParameter("per_page", 100);

        var content = await client.ExecutePaginatedWithErrorHandling<UserResponse>(request, cancellationToken);
        return content
            .OrderBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Id.ToString(), x => $"{x.Username} ({x.Name})");
    }
}
