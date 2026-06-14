namespace N2.Circulation.Api.Models;

public sealed class FineCharge
{
    public Guid Id { get; set; }

    public Guid BorrowTransactionId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string? CardNumber { get; set; }

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }
}