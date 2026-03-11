using Apps.GitLab.Constants;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication.OAuth2;
using Blackbird.Applications.Sdk.Common.Connections;
using Blackbird.Applications.Sdk.Common.Invocation;
using Microsoft.AspNetCore.WebUtilities;

namespace Apps.Gitlab.Auth.OAuth2;

public class OAuth2AuthorizeService : BaseInvocable, IOAuth2AuthorizeService
{
    public OAuth2AuthorizeService(InvocationContext invocationContext) : base(invocationContext)
    {
    }

    public string GetAuthorizationUrl(Dictionary<string, string> values)
    {
        string bridgeOauthUrl = $"{InvocationContext.UriInfo.BridgeServiceUrl.ToString().TrimEnd('/')}/oauth";

        var connectionType = GetConnectionType(values);
        var authorizationUrl = GetAuthorizationEndpoint(values, connectionType);
        var clientId = GetClientId(values, connectionType);

        var parameters = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "redirect_uri", $"{InvocationContext.UriInfo.BridgeServiceUrl.ToString().TrimEnd('/')}/AuthorizationCode" },
            { "scope", ApplicationConstants.Scope },
            { "state", values["state"] },
            { "response_type", "code" },
            { "authorization_url", authorizationUrl },
            { "actual_redirect_uri", InvocationContext.UriInfo.AuthorizationCodeRedirectUri.ToString() },
        };

        return QueryHelpers.AddQueryString(bridgeOauthUrl, parameters);
    }

    private static string GetConnectionType(Dictionary<string, string> values)
    {
        var connectionType = values[nameof(ConnectionPropertyGroup)];

        return ConnectionTypes.SupportedConnectionTypes.Contains(connectionType)
            ? connectionType
            : throw new Exception($"Unsupported connection type: {connectionType}");
    }

    private static string GetAuthorizationEndpoint(Dictionary<string, string> values, string connectionType)
    {
        return connectionType switch
        {
            ConnectionTypes.OAuth => "https://gitlab.com/oauth/authorize",
            ConnectionTypes.OAuthSelfManaged => $"{values[CredNames.BaseUrl].TrimEnd('/')}/oauth/authorize",
            _ => throw new Exception($"Unsupported connection type for OAuth authorization: {connectionType}")
        };
    }

    private static string GetClientId(Dictionary<string, string> values, string connectionType)
    {
        return connectionType switch
        {
            ConnectionTypes.OAuth => ApplicationConstants.ClientId,
            ConnectionTypes.OAuthSelfManaged => values[CredNames.ClientId],
            _ => throw new Exception($"Unsupported connection type for OAuth authorization: {connectionType}")
        };
    }
}