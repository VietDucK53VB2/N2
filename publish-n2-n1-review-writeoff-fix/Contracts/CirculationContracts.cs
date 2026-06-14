namespace N2.Circulation.Api.Contracts;

public sealed record BorrowRequest
{
    public string BookId { get; init; } = string.Empty;

    public string? Isbn { get; init; }

    public string? UserId { get; init; }

    public string? CardNumber { get; init; }

    public string? ReaderName { get; init; }

    public string? ReaderUsername { get; init; }

    public DateTime? BorrowedAt { get; init; }

    public DateTime? DueAt { get; init; }

    public int Quantity { get; init; } = 1;
}

public sealed record BorrowPolicyRequest
{
    public int MonthlyBorrowLimit { get; init; } = 5;
}

public sealed record ReturnRequest
{
    public string BookId { get; init; } = string.Empty;

    public string? Isbn { get; init; }

    public string? UserId { get; init; }

    public string? CardNumber { get; init; }

    public DateTime? ReturnedAt { get; init; }
}

public sealed record ReturnApprovalRequest
{
    public string Condition { get; init; } = "Good";

    public string? ConditionNote { get; init; }
}

public sealed record BookReviewRequest
{
    public string? UserId { get; init; }

    public string? CardNumber { get; init; }

    public string? Username { get; init; }

    public string? FullName { get; init; }

    public int Rating { get; init; }

    public string? Comment { get; init; }

    public DateTimeOffset? CreatedAt { get; init; }
}

public sealed record BorrowResponse
{
    public Guid TransactionId { get; init; }

    public string BookId { get; init; } = string.Empty;

    public string? Isbn { get; init; }

    public string? UserId { get; init; }

    public string? CardNumber { get; init; }

    public string? ReaderName { get; init; }

    public string? ReaderUsername { get; init; }

    public DateTime BorrowedAt { get; init; }

    public DateTime DueAt { get; init; }
}

public sealed record ReturnResponse
{
    public Guid TransactionId { get; init; }

    public string BookId { get; init; } = string.Empty;

    public string? Isbn { get; init; }

    public string? UserId { get; init; }

    public string? CardNumber { get; init; }

    public DateTime BorrowedAt { get; init; }

    public DateTime ReturnedAt { get; init; }

    public decimal FineAmount { get; init; }

    public string Condition { get; init; } = "Good";

    public bool ReturnedToCatalog { get; init; } = true;
}

public sealed record MonthlyCirculationStatsDto
{
    public string Month { get; init; } = string.Empty;

    public int BorrowCount { get; init; }

    public int ReturnCount { get; init; }
}

public sealed record PopularBookStatsDto
{
    public string BookId { get; init; } = string.Empty;

    public string? Title { get; init; }

    public string? Author { get; init; }

    public int BorrowCount { get; init; }
}

public sealed record BookBorrowedEvent
{
    public string EventType { get; init; } = "book.borrowed";

    public string BookId { get; init; } = string.Empty;

    public string? Isbn { get; init; }

    public string? UserId { get; init; }

    public string? CardNumber { get; init; }

    public string? ReaderName { get; init; }

    public string? ReaderUsername { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}

public sealed record BookReturnedEvent
{
    public string EventType { get; init; } = "book.returned";

    public string BookId { get; init; } = string.Empty;

    public string? Isbn { get; init; }

    public string? UserId { get; init; }

    public string? CardNumber { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public decimal FineAmount { get; init; }
}

public sealed record BookAvailabilityChangedEvent
{
    public string EventType { get; init; } = "book.availability.changed";

    public string BookId { get; init; } = string.Empty;

    public string? Isbn { get; init; }

    public int AvailableCopies { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}
