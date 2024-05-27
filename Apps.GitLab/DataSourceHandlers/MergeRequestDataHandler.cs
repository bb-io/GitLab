using Apps.Gitlab.Models.Respository.Requests;
using Apps.Gitlab;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common;
using GitLabApiClient.Internal.Paths;

namespace Apps.GitLab.DataSourceHandlers;

public class MergeRequestDataHandler : BaseInvocable, IAsyncDataSourceHandler
{
    private IEnumerable<AuthenticationCredentialsProvider> Creds =>
        InvocationContext.AuthenticationCredentialsProviders;

    private GetRepositoryRequest RepositoryRequest { get; set; }

    public MergeRequestDataHandler(InvocationContext invocationContext, [ActionParameter] GetRepositoryRequest repositoryRequest) : base(invocationContext)
    {
        RepositoryRequest = repositoryRequest;
    }

    public async Task<Dictionary<string, string>> GetDataAsync(
        DataSourceContext context,
        CancellationToken cancellationToken)
    {
        if (RepositoryRequest == null || string.IsNullOrWhiteSpace(RepositoryRequest.RepositoryId))
            throw new ArgumentException("Please, specify repository first");
        var projectId = (ProjectId)int.Parse(RepositoryRequest.RepositoryId);
        var mergeRequests = await new BlackbirdGitlabClient(Creds).Client.MergeRequests.GetAsync(projectId, (options) => { });

        return mergeRequests
            .Where(x => context.SearchString == null ||
                        x.Title.Contains(context.SearchString, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToDictionary(x => x.Iid.ToString(), x => x.Title);
    }
}