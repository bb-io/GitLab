using Apps.GitLab.Constants;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Connections;

namespace Apps.Gitlab.Connections;

public class ConnectionDefinition : IConnectionDefinition
{
    public IEnumerable<ConnectionPropertyGroup> ConnectionPropertyGroups => new List<ConnectionPropertyGroup>
    {
        new ConnectionPropertyGroup
        {
            DisplayName = "OAuth",
            Name = ConnectionTypes.OAuth,
            AuthenticationType = ConnectionAuthenticationType.OAuth2,
            ConnectionProperties = new List<ConnectionProperty>()
        },
        new ConnectionPropertyGroup
        {
            DisplayName = "OAuth Self-managed",
            Name = ConnectionTypes.OAuthSelfManaged,
            AuthenticationType = ConnectionAuthenticationType.OAuth2,
            ConnectionProperties = new List<ConnectionProperty>
            {
                new(CredNames.BaseUrl) { DisplayName = "Base URL" },
                new(CredNames.ClientId) { DisplayName = "Client ID" },
                new(CredNames.ClientSecret) { DisplayName = "Client secret", Sensitive = true },
            }
        },
        new ConnectionPropertyGroup
        {
            DisplayName = "Personal Access Token",
            Name = ConnectionTypes.PersonalAccessToken,
            AuthenticationType = ConnectionAuthenticationType.Undefined,
            ConnectionProperties = new List<ConnectionProperty>
            {
                new(CredNames.ApiKey) { Sensitive = true, DisplayName = "API key" }
            }
        }
    };

    public IEnumerable<AuthenticationCredentialsProvider> CreateAuthorizationCredentialsProviders(Dictionary<string, string> values)
    {
        var providers = values
            .Select(x => new AuthenticationCredentialsProvider(x.Key, x.Value))
            .ToList();

        var connectionType = values[nameof(ConnectionPropertyGroup)] switch
        {
            var ct when ConnectionTypes.SupportedConnectionTypes.Contains(ct) => ct,
            _ => throw new Exception($"Unknown connection type: {values[nameof(ConnectionPropertyGroup)]}")
        };

        providers.Add(new AuthenticationCredentialsProvider(CredNames.ConnectionType, connectionType));

        var authorizationToken = GetAuthorizationToken(values);
        if (!string.IsNullOrWhiteSpace(authorizationToken))
        {
            providers.Add(new AuthenticationCredentialsProvider(CredNames.Authorization, authorizationToken));
        }

        return providers;
    }

    private static string? GetAuthorizationToken(Dictionary<string, string> values)
    {
        if (values.TryGetValue("access_token", out var accessToken))
            return accessToken;

        return values.TryGetValue(CredNames.ApiKey, out var apiKey)
            ? apiKey
            : null;
    }
}
