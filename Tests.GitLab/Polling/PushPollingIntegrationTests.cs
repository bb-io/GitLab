using Apps.Gitlab;
using Apps.GitLab.Constants;
using Apps.GitLab.Polling;
using Apps.GitLab.Polling.Models;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Polling;

namespace Tests.GitLab.Polling;

[TestClass]
public class PushPollingIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task ReporterConnection_CompletesReadOnlyHappyPath()
    {
        var token = Environment.GetEnvironmentVariable("GITLAB_REPORTER_TEST_TOKEN");
        var repositoryId = Environment.GetEnvironmentVariable("GITLAB_REPORTER_TEST_REPOSITORY_ID");
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(repositoryId))
            Assert.Inconclusive(
                "Set GITLAB_REPORTER_TEST_TOKEN and GITLAB_REPORTER_TEST_REPOSITORY_ID to run Reporter-role test.");

        var credentials = new AuthenticationCredentialsProvider[]
        {
            new(CredNames.ConnectionType, ConnectionTypes.PersonalAccessToken),
            new(CredNames.Authorization, token!)
        };
        var context = new InvocationContext { AuthenticationCredentialsProviders = credentials };
        var now = DateTimeOffset.UtcNow;
        var polling = new PushPollingList(context, new BlackbirdGitlabClient(credentials), () => now);
        var input = new FilesModifiedPollingInput { RepositoryIds = [repositoryId!] };
        var baseline = await polling.OnFilesModifiedAcrossRepositories(
            new PollingEventRequest<PushPollingMemory> { PollingTime = now.UtcDateTime }, input);
        baseline.Memory!.RecentProcessedKeys = [];

        var response = await polling.OnFilesModifiedAcrossRepositories(
            new PollingEventRequest<PushPollingMemory>
            {
                Memory = baseline.Memory,
                PollingTime = now.UtcDateTime
            }, input);

        Assert.IsTrue(response.FlyBird,
            "Reporter fixture repository needs a normal branch push from the previous 24 hours.");
        Assert.IsNotNull(response.Result);
        Assert.IsTrue(response.Result.Pushes.Any(push => push.RepositoryId.ToString() == repositoryId));
    }
}
