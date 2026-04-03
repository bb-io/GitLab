using Apps.Gitlab;
using Apps.Gitlab.Models.Respository.Requests;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using Apps.GitLab.Utils;
using GitLabApiClient.Models.MergeRequests.Responses;
using RestSharp;

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

        var projectId = ParsingUtils.ParseIntOrThrow(RepositoryRequest.RepositoryId, "Repository ID");
        var client = new BlackbirdGitlabClient(Creds);
        var request = client.CreateRequest($"/projects/{projectId}/merge_requests", Method.Get);
        var mergeRequests = await client.ExecuteWithErrorHandling<List<MergeRequest>>(request);

        return mergeRequests
            .Where(x => context.SearchString == null ||
                        x.Title.Contains(context.SearchString, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToDictionary(x => x.Iid.ToString(), x => x.Title);
    }
}
