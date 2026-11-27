using Apps.Gitlab.DataSourceHandlers;
using Apps.Gitlab.Models.Respository.Requests;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.GitLab.Configuration;

public class ConfigurationBranchDataHandler(
    InvocationContext invocationContext,
    [ActionParameter] ConfigurationRepositoryInput repository)
    : BranchDataHandler(invocationContext, new GetRepositoryRequest { RepositoryId = repository.RepositoryId });
