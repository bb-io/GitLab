using Apps.GitLab.Constants;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Authentication.OAuth2;
using Blackbird.Applications.Sdk.Common.Connections;
using Blackbird.Applications.Sdk.Common.Invocation;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.Json;

namespace Apps.Gitlab.Auth.OAuth2;

public class OAuth2TokenService : BaseInvocable, IOAuth2TokenService, ITokenRefreshable
{
    private const string ExpiresAtKeyName = "expires_at";

    public OAuth2TokenService(InvocationContext invocationContext) : base(invocationContext)
    {
    }

    public bool IsRefreshToken(Dictionary<string, string> values)
        => values.TryGetValue(ExpiresAtKeyName, out var expireValue) &&
           DateTime.UtcNow > DateTime.Parse(expireValue);

    public int? GetRefreshTokenExprireInMinutes(Dictionary<string, string> values)
    {
        if (!values.TryGetValue(ExpiresAtKeyName, out var expireValue))
            return null;

        if (!DateTime.TryParse(expireValue, out var expireDate))
            return null;

        var difference = expireDate - DateTime.UtcNow;

        return (int)difference.TotalMinutes - 5;
    }

    public async Task<Dictionary<string, string>> RefreshToken(
        Dictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        const string grantType = "refresh_token";

        var connectionType = GetConnectionType(values);

        var bodyParameters = new Dictionary<string, string>
        {
            { "grant_type", grantType },
            { "client_id", GetClientId(values, connectionType) },
            { "client_secret", GetClientSecret(values, connectionType) },
            { "refresh_token", values["refresh_token"] },
            { "redirect_uri", $"{InvocationContext.UriInfo.BridgeServiceUrl.ToString().TrimEnd('/')}/AuthorizationCode" },
        };

        var tokenUrl = GetTokenEndpoint(values, connectionType);
        return await RequestToken(tokenUrl, bodyParameters, cancellationToken);
    }

    public async Task<Dictionary<string, string?>> RequestToken(
        string state,
        string code,
        Dictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        const string grantType = "authorization_code";

        var connectionType = GetConnectionType(values);

        var bodyParameters = new Dictionary<string, string>
        {
            { "grant_type", grantType },
            { "client_id", GetClientId(values, connectionType) },
            { "client_secret", GetClientSecret(values, connectionType) },
            { "code", code },
            { "redirect_uri", $"{InvocationContext.UriInfo.BridgeServiceUrl.ToString().TrimEnd('/')}/AuthorizationCode" },
        };

        var tokenUrl = GetTokenEndpoint(values, connectionType);
        var result = await RequestToken(tokenUrl, bodyParameters, cancellationToken);

        return result.ToDictionary(x => x.Key, x => (string?)x.Value);
    }

    public Task RevokeToken(Dictionary<string, string> values)
    {
        throw new NotImplementedException();
    }

    private async Task<Dictionary<string, string>> RequestToken(
        string tokenUrl,
        Dictionary<string, string> bodyParameters,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        var url = QueryHelpers.AddQueryString(tokenUrl, bodyParameters);
        using var response = await httpClient.PostAsync(url, null, cancellationToken);

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"GitLab token request failed. Status: {(int)response.StatusCode}. Response: {responseContent}");

        var resultDictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent)?
            .ToDictionary(x => x.Key, x => x.Value?.ToString())
            ?? throw new InvalidOperationException($"Invalid response content: {responseContent}");

        if (!resultDictionary.TryGetValue("expires_in", out var expiresInValue) || string.IsNullOrWhiteSpace(expiresInValue))
            return resultDictionary!;

        var expiresIn = int.Parse(expiresInValue);
        var expiresAt = utcNow.AddSeconds(expiresIn);
        resultDictionary[ExpiresAtKeyName] = expiresAt.ToString();

        return resultDictionary!;
    }

    private static string GetConnectionType(Dictionary<string, string> values)
    {
        var connectionType = values[nameof(ConnectionPropertyGroup)];

        return ConnectionTypes.SupportedConnectionTypes.Contains(connectionType)
            ? connectionType
            : throw new Exception($"Unsupported connection type: {connectionType}");
    }

    private static string GetTokenEndpoint(Dictionary<string, string> values, string connectionType)
    {
        return connectionType switch
        {
            ConnectionTypes.OAuth => "https://gitlab.com/oauth/token",
            ConnectionTypes.OAuthSelfManaged => $"{values[CredNames.BaseUrl].TrimEnd('/')}/oauth/token",
            _ => throw new Exception($"Unsupported connection type for OAuth token exchange: {connectionType}")
        };
    }

    private static string GetClientId(Dictionary<string, string> values, string connectionType)
    {
        return connectionType switch
        {
            ConnectionTypes.OAuth => ApplicationConstants.ClientId,
            ConnectionTypes.OAuthSelfManaged => values[CredNames.ClientId],
            _ => throw new Exception($"Unsupported connection type for OAuth client id: {connectionType}")
        };
    }

    private static string GetClientSecret(Dictionary<string, string> values, string connectionType)
    {
        return connectionType switch
        {
            ConnectionTypes.OAuth => ApplicationConstants.ClientSecret,
            ConnectionTypes.OAuthSelfManaged => values[CredNames.ClientSecret],
            _ => throw new Exception($"Unsupported connection type for OAuth client secret: {connectionType}")
        };
    }
}