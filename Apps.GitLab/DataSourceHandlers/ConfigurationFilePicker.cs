using Apps.Gitlab.DataSourceHandlers;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Respository.Requests;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.GitLab.Configuration;

public class ConfigurationFilePicker(
    InvocationContext invocationContext,
    [ActionParameter] GetRepositoryRequest repository,
    [ActionParameter] GetBranchRequest branch)
    : FilePickerDataHandler(invocationContext, repository, new GetOptionalBranchRequest { Name = branch.Name });
