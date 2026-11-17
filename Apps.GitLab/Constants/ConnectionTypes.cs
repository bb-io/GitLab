namespace Apps.GitLab.Constants
{
    public static class ConnectionTypes
    {
        public const string OAuth = "OAuth";
        public const string PersonalAccessToken = "Personal Access Token";
        public const string PersonalAccessTokenSelfManaged = "Personal Access Token Self-managed";
        public const string OAuthSelfManaged = "OAuth Self-managed";

        public static readonly IEnumerable<string> SupportedConnectionTypes = [OAuth, OAuthSelfManaged, PersonalAccessToken, PersonalAccessTokenSelfManaged];
    }
}
