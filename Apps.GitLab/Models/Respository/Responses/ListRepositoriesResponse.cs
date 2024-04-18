using Apps.Gitlab.Dtos;

namespace Apps.Gitlab.Models.Respository.Responses;

public record ListRepositoriesResponse(RepositoryDto[] Repositories);