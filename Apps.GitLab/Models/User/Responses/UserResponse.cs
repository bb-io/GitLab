namespace Apps.GitLab.Models.User.Responses;

public class UserResponse
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;
}
