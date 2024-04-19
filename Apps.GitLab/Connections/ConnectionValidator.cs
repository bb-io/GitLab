using Apps.Gitlab.Actions;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Connections;

namespace Apps.Gitlab.Connections
{
    public class ConnectionValidator : IConnectionValidator
    {
        public async ValueTask<ConnectionValidationResponse> ValidateConnection(
            IEnumerable<AuthenticationCredentialsProvider> authProviders, CancellationToken cancellationToken)
        {
            try
            {
                await new BlackbirdGitlabClient(authProviders).Client.Projects.GetAsync((options) => { options.IsMemberOf = true; });

                return new()
                {
                    IsValid = true
                };
            }
            catch (Exception ex)
            {
                return new()
                {
                    IsValid = false,
                    Message = ex.Message
                };
            }
        }
    }
}