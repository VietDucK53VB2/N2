namespace N2.Circulation.Api.Models;

public sealed class BorrowTransaction
{
    public Guid Id { get; set; }

    public string BookId { get; set; } = string.Empty;

    public string? Isbn { get; set; }

    public string? UserId { get; set; }

    public string? CardNumber { get; set; }

    public DateTime BorrowedAt { get; set; }

    public DateTime DueAt { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public decimal FineAmount { get; set; }

    public string Status { get; set; } = "Pending"; // Pending, Borrowed, ReturnPending, Overdue, Returned
}
