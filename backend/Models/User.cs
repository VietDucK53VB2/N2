namespace N2.Circulation.Api.Models;

public sealed class User
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? CardNumber { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsActive { get; set; }
}
