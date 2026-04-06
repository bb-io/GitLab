using Apps.GitLab.Constants;

namespace Apps.Gitlab.Auth.OAuth2;

internal static class OAuth2UrlHelper
{
    public static string GetSelfManagedBaseUrl(Dictionary<string, string> values)
        => values[CredNames.BaseUrl].TrimEnd('/');
}
