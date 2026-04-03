using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Connections;
using Blackbird.Applications.Sdk.Common.Exceptions;
using RestSharp;

namespace Apps.Gitlab.Connections;

public class ConnectionValidator : IConnectionValidator
{
    public async ValueTask<ConnectionValidationResponse> ValidateConnection(
        IEnumerable<AuthenticationCredentialsProvider> authProviders, CancellationToken cancellationToken)
    {
        try
        {
            var client = new BlackbirdGitlabClient(authProviders);
            await client.ExecuteWithErrorHandling(client.CreateRequest("/user", Method.Get));
        }
        catch (PluginMisconfigurationException ex)
        {
            return new()
            {
                IsValid = false,
                Message = ex.Message
            };
        }
        catch (Exception)
        {
            return new()
            {
                IsValid = true
            };
        }

        return new()
        {
            IsValid = true
        };
    }
}
