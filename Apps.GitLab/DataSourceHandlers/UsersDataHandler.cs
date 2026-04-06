using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using Apps.GitLab.Models.User.Responses;
using RestSharp;

namespace Apps.Gitlab.DataSourceHandlers;

public class UsersDataHandler : BaseInvocable, IAsyncDataSourceHandler
{
    private IEnumerable<AuthenticationCredentialsProvider> Creds =>
        InvocationContext.AuthenticationCredentialsProviders;

    public UsersDataHandler(InvocationContext invocationContext) : base(invocationContext)
    {
    }

    public async Task<Dictionary<string, string>> GetDataAsync(
        DataSourceContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.SearchString))
            return new Dictionary<string, string>();

        var client = new BlackbirdGitlabClient(Creds);
        var request = client.CreateRequest("/users", Method.Get);
        request.AddQueryParameter("search", context.SearchString);

        var content = await client.ExecuteWithErrorHandling<List<UserResponse>>(request);
        return content.ToDictionary(x => x.Id.ToString(), x => x.Username);
    }
}
