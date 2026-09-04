using System.Net;
using Apps.GitLab.Polling;
using Apps.GitLab.Polling.Models;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Polling;
using Newtonsoft.Json;
using Tests.GitLab.Base;

namespace Tests.GitLab.Polling;

[TestClass]
public class PushPollingListTests
{
    [TestMethod]
    public async Task NullMemory_EstablishesBaselineAndSeedsOnlySelectedRepositoryEvents()
    {
        var handler = CreateScenarioHandler("events/mixed-pushes.json", emptyBaseline: false);
        var polling = CreatePollingList(handler);

        var response = await polling.OnFilesModifiedAcrossRepositories(
            new PollingEventRequest<PushPollingMemory> { PollingTime = GitLabTestData.ScanTime.UtcDateTime },
            CreateInput("101"));

        Assert.IsFalse(response.FlyBird);
        Assert.IsNull(response.Result);
        Assert.AreEqual(GitLabTestData.ScanTime, response.Memory!.LastCompletedScanUtc);
        Assert.IsFalse(string.IsNullOrWhiteSpace(response.Memory.InputConfigurationFingerprint));
        Assert.HasCount(1, response.Memory.RecentProcessedKeys);
        Assert.AreEqual(1001, response.Memory.RecentProcessedKeys[0].EventId);
        Assert.HasCount(1, handler.RequestUris);
        var query = handler.RequestUris.Single().Query;
        StringAssert.Contains(query, "scope=all");
        StringAssert.Contains(query, "action=pushed");
        StringAssert.Contains(query, "sort=desc");
        StringAssert.Contains(query, "per_page=100");
        StringAssert.Contains(query, "after=2026-09-02");
        StringAssert.Contains(query, "before=2026-09-05");
    }

    [TestMethod]
    public async Task MatchingCommit_ReturnsAllCommitsAndOnlyUniqueStrictlyModifiedBareFilenameMatches()
    {
        var handler = CreateScenarioHandler("events/mixed-pushes.json");
        var response = await PollAfterBaseline(
            CreatePollingList(handler),
            new FilesModifiedPollingInput
            {
                RepositoryIds = ["101"],
                IncludedCommitMessagePatterns = ["^translate"],
                FilePatterns = ["en.po"]
            });

        Assert.IsTrue(response.FlyBird);
        var push = response.Result!.Pushes.Single();
        Assert.AreEqual(101, push.RepositoryId);
        Assert.AreEqual("localization/repository-one", push.RepositoryNameWithNamespace);
        Assert.AreEqual("main", push.BranchName);
        Assert.AreEqual(42, push.PusherUserId);
        Assert.AreEqual(new DateTime(2026, 9, 4, 15, 45, 0, DateTimeKind.Utc), push.PushTime);
        CollectionAssert.AreEqual(new[] { "nested/deep/en.po" }, push.FilePathsMatched.ToArray());
        CollectionAssert.AreEqual(
            new[] { "sha-101-translate", "sha-101-docs" },
            push.Commits.Select(commit => commit.Hash).ToArray());
        Assert.AreEqual("Translator One", push.Commits.First().AuthorName);
        Assert.AreEqual("translator@example.test", push.Commits.First().AuthorEmail);
        var compareQuery = handler.RequestUris.Single(uri =>
            uri.AbsolutePath.EndsWith("/repository/compare", StringComparison.Ordinal)).Query;
        StringAssert.Contains(compareQuery, "from=from-101");
        StringAssert.Contains(compareQuery, "to=to-101");
        StringAssert.Contains(compareQuery, "straight=true");
        StringAssert.Contains(handler.RequestUris.Single(uri =>
            uri.AbsolutePath.EndsWith("sha-101-translate/diff", StringComparison.Ordinal)).Query, "per_page=100");
        Assert.IsTrue(handler.RequestUris.Any(uri => uri.AbsolutePath.EndsWith("sha-101-translate/diff")));
        Assert.IsFalse(handler.RequestUris.Any(uri => uri.AbsolutePath.EndsWith("sha-101-docs/diff")));
    }

    [TestMethod]
    public async Task AddedDeletedAndRenamedFiles_NeverQualifyAsModified()
    {
        var handler = CreateScenarioHandler("events/mixed-pushes.json");
        var response = await PollAfterBaseline(
            CreatePollingList(handler),
            new FilesModifiedPollingInput
            {
                RepositoryIds = ["101"],
                IncludedCommitMessagePatterns = ["^translate"],
                FilePatterns = ["added/only.po", "deleted/only.po", "renamed/only.po"]
            });

        Assert.IsFalse(response.FlyBird);
        Assert.IsNull(response.Result);
        Assert.AreEqual(1001, response.Memory!.RecentProcessedKeys.Single().EventId);
    }

    [TestMethod]
    public async Task MessageAndFileMustMatchSameCommit_AndRejectedEventIsNotReevaluated()
    {
        var handler = CreateScenarioHandler("events/mixed-pushes.json");
        var polling = CreatePollingList(handler);
        var input = new FilesModifiedPollingInput
        {
            RepositoryIds = ["101"],
            IncludedCommitMessagePatterns = ["^translate"],
            FilePatterns = ["other.po"]
        };

        var first = await PollAfterBaseline(polling, input);
        var enrichmentsAfterFirst = handler.RequestUris.Count(uri =>
            uri.AbsolutePath.Contains("/repository/compare", StringComparison.Ordinal) ||
            uri.AbsolutePath.Contains("/diff", StringComparison.Ordinal));
        var second = await polling.OnFilesModifiedAcrossRepositories(
            new PollingEventRequest<PushPollingMemory>
            {
                Memory = first.Memory,
                PollingTime = GitLabTestData.ScanTime.UtcDateTime
            }, input);

        Assert.IsFalse(first.FlyBird);
        Assert.IsNull(first.Result);
        Assert.IsFalse(second.FlyBird);
        Assert.IsNull(second.Result);
        Assert.AreEqual(1001, first.Memory!.RecentProcessedKeys.Single().EventId);
        Assert.AreEqual(enrichmentsAfterFirst, handler.RequestUris.Count(uri =>
            uri.AbsolutePath.Contains("/repository/compare", StringComparison.Ordinal) ||
            uri.AbsolutePath.Contains("/diff", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task MultipleMatchingPushes_ReturnOneBatchInFeedOrder()
    {
        var handler = CreateScenarioHandler("events/mixed-pushes.json");
        var response = await PollAfterBaseline(
            CreatePollingList(handler),
            new FilesModifiedPollingInput
            {
                RepositoryIds = ["101", "202"],
                FilePatterns = ["*.po"]
            });

        Assert.IsTrue(response.FlyBird);
        var pushes = response.Result!.Pushes.ToList();
        Assert.HasCount(2, pushes);
        CollectionAssert.AreEqual(new[] { 101, 202 }, pushes.Select(push => push.RepositoryId).ToArray());
        CollectionAssert.AreEqual(
            new[] { "localization/repository-one", "localization/repository-two" },
            pushes.Select(push => push.RepositoryNameWithNamespace).ToArray());
    }

    [TestMethod]
    public async Task RepositoryFilter_PreventsEnrichmentOfUnrelatedGlobalEvents()
    {
        var handler = CreateScenarioHandler("events/mixed-pushes.json");

        await PollAfterBaseline(CreatePollingList(handler), CreateInput("101"));

        Assert.IsFalse(handler.RequestUris.Any(uri =>
            uri.AbsolutePath.Contains("/projects/202/", StringComparison.Ordinal) ||
            uri.AbsolutePath.Contains("/projects/999/", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task BranchAndPusherIgnoreRulesWinOverWatchRules()
    {
        var branchHandler = CreateScenarioHandler("events/mixed-pushes.json");
        var branchResponse = await PollAfterBaseline(
            CreatePollingList(branchHandler),
            new FilesModifiedPollingInput
            {
                RepositoryIds = ["101"],
                BranchPatterns = ["^main$"],
                IgnoredBranchPatterns = ["^main$"]
            });

        var pusherHandler = CreateScenarioHandler("events/mixed-pushes.json");
        var pusherResponse = await PollAfterBaseline(
            CreatePollingList(pusherHandler),
            new FilesModifiedPollingInput
            {
                RepositoryIds = ["101"],
                PusherUserIds = ["42"],
                IgnoredPusherUserIds = ["42"]
            });

        Assert.IsFalse(branchResponse.FlyBird);
        Assert.IsFalse(pusherResponse.FlyBird);
        Assert.IsFalse(branchHandler.RequestUris.Any(uri =>
            uri.AbsolutePath.Contains("/repository/compare", StringComparison.Ordinal)));
        Assert.IsFalse(pusherHandler.RequestUris.Any(uri =>
            uri.AbsolutePath.Contains("/repository/compare", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExcludedMessageRuleWinsOverIncludedMessageRule()
    {
        var handler = CreateScenarioHandler("events/mixed-pushes.json");
        var response = await PollAfterBaseline(
            CreatePollingList(handler),
            new FilesModifiedPollingInput
            {
                RepositoryIds = ["101"],
                IncludedCommitMessagePatterns = ["translate"],
                ExcludedCommitMessagePatterns = ["translate"],
                FilePatterns = ["en.po"]
            });

        Assert.IsFalse(response.FlyBird);
        Assert.IsNull(response.Result);
        Assert.IsFalse(handler.RequestUris.Any(uri =>
            uri.AbsolutePath.Contains("/diff", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServerDayWindow_IsNarrowedToExactTimestampsLocally()
    {
        var handler = CreateScenarioHandler("events/outside-exact-window.json");
        var response = await PollAfterBaseline(CreatePollingList(handler), CreateInput("101"));

        Assert.IsFalse(response.FlyBird);
        Assert.IsNull(response.Result);
        Assert.IsEmpty(response.Memory!.RecentProcessedKeys);
        Assert.IsFalse(handler.RequestUris.Any(uri =>
            uri.AbsolutePath.Contains("/repository/compare", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Overlap_DoesNotEmitOrEnrichSameEventTwice()
    {
        var handler = CreateScenarioHandler("events/mixed-pushes.json");
        var polling = CreatePollingList(handler);
        var input = new FilesModifiedPollingInput
        {
            RepositoryIds = ["101"],
            IncludedCommitMessagePatterns = ["^translate"],
            FilePatterns = ["en.po"]
        };

        var first = await PollAfterBaseline(polling, input);
        var compareCalls = handler.RequestUris.Count(uri =>
            uri.AbsolutePath.Contains("/repository/compare", StringComparison.Ordinal));
        var second = await polling.OnFilesModifiedAcrossRepositories(
            new PollingEventRequest<PushPollingMemory>
            {
                Memory = first.Memory,
                PollingTime = GitLabTestData.ScanTime.UtcDateTime
            }, input);

        Assert.IsTrue(first.FlyBird);
        Assert.IsFalse(second.FlyBird);
        Assert.IsNull(second.Result);
        Assert.AreEqual(compareCalls, handler.RequestUris.Count(uri =>
            uri.AbsolutePath.Contains("/repository/compare", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task TransientFailure_ThrowsAndDoesNotMutateCursor()
    {
        var handler = CreateScenarioHandler(
            "events/mixed-pushes.json",
            project101CompareStatus: HttpStatusCode.ServiceUnavailable);
        var polling = CreatePollingList(handler);
        var input = CreateInput("101");
        var baseline = await polling.OnFilesModifiedAcrossRepositories(
            new PollingEventRequest<PushPollingMemory> { PollingTime = GitLabTestData.ScanTime.UtcDateTime },
            input);
        var memoryBefore = JsonConvert.SerializeObject(baseline.Memory);

        await Assert.ThrowsExactlyAsync<PluginApplicationException>(() =>
            polling.OnFilesModifiedAcrossRepositories(
                new PollingEventRequest<PushPollingMemory>
                {
                    Memory = baseline.Memory,
                    PollingTime = GitLabTestData.ScanTime.UtcDateTime
                }, input));

        Assert.AreEqual(memoryBefore, JsonConvert.SerializeObject(baseline.Memory));
    }

    [TestMethod]
    public async Task AmbiguousCompareBadRequest_ThrowsAndDoesNotMutateCursor()
    {
        var handler = CreateScenarioHandler(
            "events/mixed-pushes.json",
            project101CompareStatus: HttpStatusCode.BadRequest);
        var polling = CreatePollingList(handler);
        var input = CreateInput("101");
        var baseline = await polling.OnFilesModifiedAcrossRepositories(
            new PollingEventRequest<PushPollingMemory> { PollingTime = GitLabTestData.ScanTime.UtcDateTime },
            input);
        var memoryBefore = JsonConvert.SerializeObject(baseline.Memory);

        await Assert.ThrowsExactlyAsync<PluginApplicationException>(() =>
            polling.OnFilesModifiedAcrossRepositories(
                new PollingEventRequest<PushPollingMemory>
                {
                    Memory = baseline.Memory,
                    PollingTime = GitLabTestData.ScanTime.UtcDateTime
                }, input));

        Assert.AreEqual(memoryBefore, JsonConvert.SerializeObject(baseline.Memory));
    }

    [TestMethod]
    public async Task MissingCommitId_ThrowsAndDoesNotMutateCursor()
    {
        var handler = CreateScenarioHandler(
            "events/mixed-pushes.json",
            project101CompareFixture: "compare/project-101-missing-id.json");
        var polling = CreatePollingList(handler);
        var input = new FilesModifiedPollingInput
        {
            RepositoryIds = ["101"],
            IncludedCommitMessagePatterns = ["^translate"],
            FilePatterns = ["en.po"]
        };
        var baseline = await polling.OnFilesModifiedAcrossRepositories(
            new PollingEventRequest<PushPollingMemory> { PollingTime = GitLabTestData.ScanTime.UtcDateTime },
            input);
        var memoryBefore = JsonConvert.SerializeObject(baseline.Memory);

        await Assert.ThrowsExactlyAsync<PluginApplicationException>(() =>
            polling.OnFilesModifiedAcrossRepositories(
                new PollingEventRequest<PushPollingMemory>
                {
                    Memory = baseline.Memory,
                    PollingTime = GitLabTestData.ScanTime.UtcDateTime
                }, input));

        Assert.AreEqual(memoryBefore, JsonConvert.SerializeObject(baseline.Memory));
    }

    [TestMethod]
    public async Task MissingDiffFlags_ThrowsAndDoesNotMutateCursor()
    {
        var handler = CreateScenarioHandler(
            "events/mixed-pushes.json",
            project101DiffFixture: "diffs/sha-101-missing-flags.json");
        var polling = CreatePollingList(handler);
        var input = new FilesModifiedPollingInput
        {
            RepositoryIds = ["101"],
            IncludedCommitMessagePatterns = ["^translate"],
            FilePatterns = ["en.po"]
        };
        var baseline = await polling.OnFilesModifiedAcrossRepositories(
            new PollingEventRequest<PushPollingMemory> { PollingTime = GitLabTestData.ScanTime.UtcDateTime },
            input);
        var memoryBefore = JsonConvert.SerializeObject(baseline.Memory);

        await Assert.ThrowsExactlyAsync<PluginApplicationException>(() =>
            polling.OnFilesModifiedAcrossRepositories(
                new PollingEventRequest<PushPollingMemory>
                {
                    Memory = baseline.Memory,
                    PollingTime = GitLabTestData.ScanTime.UtcDateTime
                }, input));

        Assert.AreEqual(memoryBefore, JsonConvert.SerializeObject(baseline.Memory));
    }

    [TestMethod]
    public async Task BulkNewDeletedAndUnrecoverableForcePushes_AreSkippedAndRemembered()
    {
        var handler = CreateScenarioHandler("events/incomplete-pushes.json");
        var response = await PollAfterBaseline(
            CreatePollingList(handler),
            CreateInput("301", "302", "303", "304", "305"));

        Assert.IsFalse(response.FlyBird);
        Assert.IsNull(response.Result);
        CollectionAssert.AreEquivalent(
            new long[] { 3001, 3002, 3003, 3004, 3005 },
            response.Memory!.RecentProcessedKeys.Select(key => key.EventId).ToArray());
        Assert.HasCount(1, handler.RequestUris.Where(uri =>
            uri.AbsolutePath.Contains("/repository/compare", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task TruncatedDiff_SkipsWholePushAndStillRemembersEvent()
    {
        var handler = CreateScenarioHandler("events/truncated-diff-push.json");
        var response = await PollAfterBaseline(CreatePollingList(handler), CreateInput("401"));

        Assert.IsFalse(response.FlyBird);
        Assert.IsNull(response.Result);
        Assert.AreEqual(4001, response.Memory!.RecentProcessedKeys.Single().EventId);
        Assert.IsFalse(handler.RequestUris.Any(uri => uri.AbsolutePath == "/api/v4/projects/401"));
    }

    [TestMethod]
    public async Task InputChange_EstablishesFreshBaselineWithoutReplayingExistingActivity()
    {
        var handler = CreateScenarioHandler("events/mixed-pushes.json");
        var polling = CreatePollingList(handler);
        var firstInput = CreateInput("101");
        var baseline = await polling.OnFilesModifiedAcrossRepositories(
            new PollingEventRequest<PushPollingMemory> { PollingTime = GitLabTestData.ScanTime.UtcDateTime },
            firstInput);

        var changed = await polling.OnFilesModifiedAcrossRepositories(
            new PollingEventRequest<PushPollingMemory>
            {
                Memory = baseline.Memory,
                PollingTime = GitLabTestData.ScanTime.UtcDateTime
            }, CreateInput("202"));

        Assert.IsFalse(changed.FlyBird);
        Assert.IsNull(changed.Result);
        Assert.AreEqual(1002, changed.Memory!.RecentProcessedKeys.Single().EventId);
        Assert.AreNotEqual(
            baseline.Memory!.InputConfigurationFingerprint,
            changed.Memory.InputConfigurationFingerprint);
        Assert.IsFalse(handler.RequestUris.Any(uri =>
            uri.AbsolutePath.Contains("/repository/compare", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task PaginationSafetyCapFailure_DoesNotMutateCursor()
    {
        var eventCalls = 0;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath != "/api/v4/events")
                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");

            var call = Interlocked.Increment(ref eventCalls);
            var response = GitLabTestData.JsonFixture("events/empty.json");
            if (call > 1)
                GitLabTestData.WithHeader(response, "Link",
                    $"<https://gitlab.test/api/v4/events?page={call}>; rel=\"next\"");
            return Task.FromResult(response);
        });
        var polling = CreatePollingList(handler);
        var input = CreateInput("101");
        var baseline = await polling.OnFilesModifiedAcrossRepositories(
            new PollingEventRequest<PushPollingMemory> { PollingTime = GitLabTestData.ScanTime.UtcDateTime },
            input);
        var memoryBefore = JsonConvert.SerializeObject(baseline.Memory);

        await Assert.ThrowsExactlyAsync<PluginApplicationException>(() =>
            polling.OnFilesModifiedAcrossRepositories(
                new PollingEventRequest<PushPollingMemory>
                {
                    Memory = baseline.Memory,
                    PollingTime = GitLabTestData.ScanTime.UtcDateTime
                }, input));

        Assert.AreEqual(memoryBefore, JsonConvert.SerializeObject(baseline.Memory));
        Assert.AreEqual(201, eventCalls);
    }

    private static FilesModifiedPollingInput CreateInput(params string[] repositoryIds) => new()
    {
        RepositoryIds = repositoryIds
    };

    private static PushPollingList CreatePollingList(StubHttpMessageHandler handler) =>
        new(GitLabTestData.CreateContext(), GitLabTestData.CreateClient(handler), () => GitLabTestData.ScanTime);

    private static async Task<PollingEventResponse<PushPollingMemory, FilesModifiedPollingResponse>>
        PollAfterBaseline(PushPollingList polling, FilesModifiedPollingInput input)
    {
        var baseline = await polling.OnFilesModifiedAcrossRepositories(
            new PollingEventRequest<PushPollingMemory> { PollingTime = GitLabTestData.ScanTime.UtcDateTime },
            input);
        Assert.IsFalse(baseline.FlyBird);
        Assert.IsNull(baseline.Result);

        return await polling.OnFilesModifiedAcrossRepositories(
            new PollingEventRequest<PushPollingMemory>
            {
                Memory = baseline.Memory,
                PollingTime = GitLabTestData.ScanTime.UtcDateTime
            }, input);
    }

    private static StubHttpMessageHandler CreateScenarioHandler(
        string eventsFixture,
        bool emptyBaseline = true,
        HttpStatusCode? project101CompareStatus = null,
        string project101CompareFixture = "compare/project-101.json",
        string project101DiffFixture = "diffs/sha-101-translate.json")
    {
        var eventCalls = 0;
        return new StubHttpMessageHandler((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            HttpResponseMessage response;
            if (path == "/api/v4/events")
            {
                var call = Interlocked.Increment(ref eventCalls);
                response = GitLabTestData.JsonFixture(
                    emptyBaseline && call == 1 ? "events/empty.json" : eventsFixture);
            }
            else
            {
                response = path switch
                {
                    "/api/v4/projects/101/repository/compare" when project101CompareStatus is not null =>
                        GitLabTestData.Json("{\"message\":\"comparison unavailable\"}",
                            project101CompareStatus.Value),
                    "/api/v4/projects/101/repository/compare" =>
                        GitLabTestData.JsonFixture(project101CompareFixture),
                    "/api/v4/projects/101/repository/commits/sha-101-translate/diff" =>
                        GitLabTestData.JsonFixture(project101DiffFixture),
                    "/api/v4/projects/101/repository/commits/sha-101-docs/diff" =>
                        GitLabTestData.JsonFixture("diffs/sha-101-docs.json"),
                    "/api/v4/projects/101" => GitLabTestData.JsonFixture("projects/project-101.json"),
                    "/api/v4/projects/202/repository/compare" =>
                        GitLabTestData.JsonFixture("compare/project-202.json"),
                    "/api/v4/projects/202/repository/commits/sha-202-release/diff" =>
                        GitLabTestData.JsonFixture("diffs/sha-202-release.json"),
                    "/api/v4/projects/202" => GitLabTestData.JsonFixture("projects/project-202.json"),
                    "/api/v4/projects/304/repository/compare" =>
                        GitLabTestData.Json("{\"message\":\"404 Commit Not Found\"}", HttpStatusCode.NotFound),
                    "/api/v4/projects/401/repository/compare" =>
                        GitLabTestData.JsonFixture("compare/project-401.json"),
                    "/api/v4/projects/401/repository/commits/sha-401-large/diff" =>
                        GitLabTestData.JsonFixture("diffs/sha-401-large.json"),
                    _ => throw new InvalidOperationException($"Unexpected request: {request.RequestUri}")
                };
            }

            return Task.FromResult(response);
        });
    }
}
