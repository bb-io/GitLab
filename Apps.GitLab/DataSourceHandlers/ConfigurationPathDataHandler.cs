using Apps.Gitlab.DataSourceHandlers;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Respository.Requests;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.GitLab.Configuration;

public class ConfigurationPathDataHandler(
    InvocationContext invocationContext,
    [ActionParameter] ConfigurationRepositoryInput repository,
    [ActionParameter] ConfigurationBranchInput branch)
    : FilePickerDataHandler(invocationContext,
        new GetRepositoryRequest { RepositoryId = repository.RepositoryId },
        new GetOptionalBranchRequest { Name = branch.Name });
