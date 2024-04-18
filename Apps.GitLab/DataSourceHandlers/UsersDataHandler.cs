using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.Gitlab.DataSourceHandlers
{
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

            var content = await new BlackbirdGitlabClient(Creds).Client.Users.GetAsync();
            return content.Take(30).ToDictionary(x => x.Id.ToString(), x => $"{x.Username}");
        }
    }
}
