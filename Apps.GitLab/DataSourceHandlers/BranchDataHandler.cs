using Apps.Gitlab.Models.Respository.Requests;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using GitLabApiClient.Internal.Paths;

namespace Apps.Gitlab.DataSourceHandlers;

public class BranchDataHandler : BaseInvocable, IAsyncDataSourceHandler
{
    private IEnumerable<AuthenticationCredentialsProvider> Creds =>
        InvocationContext.AuthenticationCredentialsProviders;

    private GetRepositoryRequest RepositoryRequest { get; set; }

    public BranchDataHandler(InvocationContext invocationContext, [ActionParameter] GetRepositoryRequest repositoryRequest) : base(invocationContext)
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
        var branches = await new BlackbirdGitlabClient(Creds).Client.Branches.GetAsync(projectId, (options) => { });

        return branches
            .Where(x => context.SearchString == null ||
                        x.Name.Contains(context.SearchString, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToDictionary(x => x.Name, x => x.Name);
    }
}