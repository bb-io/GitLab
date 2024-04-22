using Newtonsoft.Json;

namespace Apps.Gitlab.Webhooks.Payloads;

public class PushPayload
{
    [JsonProperty("object_kind")]
    public string ObjectKind { get; set; }

    [JsonProperty("event_name")]
    public string EventName { get; set; }

    [JsonProperty("before")]
    public string Before { get; set; }

    [JsonProperty("after")]
    public string After { get; set; }

    [JsonProperty("ref")]
    public string Ref { get; set; }

    [JsonProperty("ref_protected")]
    public bool RefProtected { get; set; }

    [JsonProperty("checkout_sha")]
    public string CheckoutSha { get; set; }

    [JsonProperty("message")]
    public object Message { get; set; }

    [JsonProperty("user_id")]
    public int UserId { get; set; }

    [JsonProperty("user_name")]
    public string UserName { get; set; }

    [JsonProperty("user_username")]
    public string UserUsername { get; set; }

    [JsonProperty("user_email")]
    public object UserEmail { get; set; }

    [JsonProperty("user_avatar")]
    public string UserAvatar { get; set; }

    [JsonProperty("project_id")]
    public int ProjectId { get; set; }

    [JsonProperty("project")]
    public Project Project { get; set; }

    [JsonProperty("commits")]
    public List<Commit> Commits { get; set; }

    [JsonProperty("total_commits_count")]
    public int TotalCommitsCount { get; set; }

    [JsonProperty("push_options")]
    public PushOptions PushOptions { get; set; }

    [JsonProperty("repository")]
    public Repository Repository { get; set; }
}

public class Author
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("email")]
    public string Email { get; set; }
}

public class Commit
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; }

    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("author")]
    public Author Author { get; set; }

    [JsonProperty("added")]
    public List<string> Added { get; set; }

    [JsonProperty("modified")]
    public List<string> Modified { get; set; }

    [JsonProperty("removed")]
    public List<string> Removed { get; set; }
}

public class Project
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("description")]
    public object Description { get; set; }

    [JsonProperty("web_url")]
    public string WebUrl { get; set; }

    [JsonProperty("avatar_url")]
    public object AvatarUrl { get; set; }

    [JsonProperty("git_ssh_url")]
    public string GitSshUrl { get; set; }

    [JsonProperty("git_http_url")]
    public string GitHttpUrl { get; set; }

    [JsonProperty("namespace")]
    public string Namespace { get; set; }

    [JsonProperty("visibility_level")]
    public int VisibilityLevel { get; set; }

    [JsonProperty("path_with_namespace")]
    public string PathWithNamespace { get; set; }

    [JsonProperty("default_branch")]
    public string DefaultBranch { get; set; }

    [JsonProperty("ci_config_path")]
    public string CiConfigPath { get; set; }

    [JsonProperty("homepage")]
    public string Homepage { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("ssh_url")]
    public string SshUrl { get; set; }

    [JsonProperty("http_url")]
    public string HttpUrl { get; set; }
}

public class PushOptions
{
}

public class Repository
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("description")]
    public object Description { get; set; }

    [JsonProperty("homepage")]
    public string Homepage { get; set; }

    [JsonProperty("git_http_url")]
    public string GitHttpUrl { get; set; }

    [JsonProperty("git_ssh_url")]
    public string GitSshUrl { get; set; }

    [JsonProperty("visibility_level")]
    public int VisibilityLevel { get; set; }
}

