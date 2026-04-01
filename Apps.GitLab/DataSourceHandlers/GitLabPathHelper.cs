namespace Apps.Gitlab.DataSourceHandlers;

public static class GitLabPathHelper
{
    public const string RootId = "root";
    public const string RootDisplayName = "Root";

    public static string NormalizeFolderId(string? folderId)
    {
        if (string.IsNullOrWhiteSpace(folderId) || folderId == RootId || folderId == "/")
            return string.Empty;

        return folderId.Trim().Trim('/');
    }

    public static string NormalizePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Trim().Trim('/');
    }
}
