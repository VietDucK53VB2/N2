namespace N2.Circulation.Api.Models;

public sealed class RevenueRecord
{
    public Guid Id { get; set; }

    public string SourceType { get; set; } = string.Empty;

    public string ReferenceId { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public string? CardNumber { get; set; }

    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
