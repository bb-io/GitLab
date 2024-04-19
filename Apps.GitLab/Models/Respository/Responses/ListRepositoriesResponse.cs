using Apps.Gitlab.Dtos;
using GitLabApiClient.Models.Projects.Responses;

namespace Apps.Gitlab.Models.Respository.Responses;

public record ListRepositoriesResponse(Project[] Repositories);