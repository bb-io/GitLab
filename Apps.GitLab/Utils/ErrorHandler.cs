using Blackbird.Applications.Sdk.Common.Exceptions;
using GitLabApiClient;

namespace Apps.GitLab.Utils;

public static class ErrorHandler
{
    public static async Task ExecuteWithErrorHandlingAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (GitLabException ex)
        {
            throw MapGitLabException(ex);
        }
        catch (Exception ex)
        {
            throw new PluginApplicationException(ex.Message);
        }
    }

    public static async Task<T> ExecuteWithErrorHandlingAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (GitLabException ex)
        {
            throw MapGitLabException(ex);
        }
        catch (Exception ex)
        {
            throw new PluginApplicationException(ex.Message);
        }
    }

    private static Exception MapGitLabException(GitLabException ex)
    {
        var statusCode = TryGetStatusCode(ex);

        return statusCode switch
        {
            401 or 403 => new PluginMisconfigurationException(
                "GitLab credentials are invalid or do not have sufficient permissions. Please check your access token and scopes."),
            404 => new PluginApplicationException("Resource was not found in GitLab. Please verify the repository ID / path / branch."),
            429 => new PluginApplicationException("GitLab rate limit reached. Please try again later."),
            _ => new PluginApplicationException(ex.Message)
        };
    }

    private static int? TryGetStatusCode(GitLabException ex)
    {
        var type = ex.GetType();

        var statusCodeProp = type.GetProperty("StatusCode");
        if (statusCodeProp?.GetValue(ex) is int sc1) return sc1;

        var statusProp = type.GetProperty("Status");
        if (statusProp?.GetValue(ex) is int sc2) return sc2;

        if (statusCodeProp?.GetValue(ex) is System.Net.HttpStatusCode enumSc)
            return (int)enumSc;

        return null;
    }
}
