using Apps.Gitlab.DataSourceHandlers;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Respository.Requests;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.GitLab.Configuration;

public class ConfigurationPathDataHandler(
    InvocationContext invocationContext,
    [ActionParameter] ConfigurationBranchInput location)
    : FilePickerDataHandler(invocationContext,
        new GetRepositoryRequest { RepositoryId = location.RepositoryId },
        new GetOptionalBranchRequest { Name = location.Name });
