using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication.OAuth2;
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
        string bridgeOauthUrl = "https://2ae7-178-211-106-141.ngrok-free.app/api/oauth";//$"{InvocationContext.UriInfo.BridgeServiceUrl.ToString().TrimEnd('/')}/oauth";
        const string oauthUrl = "https://gitlab.com/oauth/authorize";
        var parameters = new Dictionary<string, string>
        {
            { "client_id", ApplicationConstants.ClientId.Split(' ')[0] },//ApplicationConstants.ClientId },
            { "redirect_uri", "https://2ae7-178-211-106-141.ngrok-free.app/api/AuthorizationCode" },//$"{InvocationContext.UriInfo.BridgeServiceUrl.ToString().TrimEnd('/')}/AuthorizationCode" },
            { "scope", ApplicationConstants.Scope },
            { "state", values["state"] },
            { "response_type", "code" },
            { "authorization_url", oauthUrl},
            { "actual_redirect_uri", InvocationContext.UriInfo.AuthorizationCodeRedirectUri.ToString() },
        };
        return QueryHelpers.AddQueryString(bridgeOauthUrl, parameters);
    }
}