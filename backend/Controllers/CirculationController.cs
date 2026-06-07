using System.Net.Http.Json;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using N2.Circulation.Api.Contracts;
using N2.Circulation.Api.Data;
using N2.Circulation.Api.Models;

namespace N2.Circulation.Api.Controllers;

[ApiController]
[Route("api/circulation")]
public sealed class CirculationController : ControllerBase
{
    private const decimal DailyFineRate = 5000m;
    private const int DefaultBorrowDays = 14;
    private readonly CirculationDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public CirculationController(CirculationDbContext dbContext, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpPost("borrow")]
    public async Task<ActionResult<BorrowResponse>> BorrowAsync([FromBody] BorrowRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateReference(request.BookId, request.Isbn, request.UserId, request.CardNumber);
        if (validationError is not null)
        {
            return BadRequest(new { Message = validationError });
        }

        var borrowedAt = request.BorrowedAt ?? DateTime.UtcNow;
        var dueAt = request.DueAt ?? borrowedAt.AddDays(DefaultBorrowDays);
        var quantity = Math.Clamp(request.Quantity, 1, 20);

        // Resolve actual BookId: prefer explicit BookId, otherwise look up by ISBN
        var resolvedBookId = request.BookId;
        if (string.IsNullOrWhiteSpace(resolvedBookId) && !string.IsNullOrWhiteSpace(request.Isbn))
        {
            var bookId = await GetBookIdByIsbnAsync(request.Isbn, cancellationToken);
            if (bookId is null)
            {
                return NotFound(new { Message = $"Book with ISBN '{request.Isbn}' not found in catalog." });
            }
            resolvedBookId = bookId.Value.ToString();
        }

        var transactions = Enumerable.Range(0, quantity)
            .Select(_ => new BorrowTransaction
            {
                Id = Guid.NewGuid(),
                BookId = resolvedBookId,
                Isbn = request.Isbn,
                UserId = request.UserId,
                CardNumber = request.CardNumber,
                BorrowedAt = borrowedAt,
                DueAt = dueAt,
                FineAmount = 0m,
                Status = "Pending"
            })
            .ToList();
        var transaction = transactions[0];

        var borrowedEvent = new BookBorrowedEvent
        {
            BookId = transaction.BookId,
            Isbn = transaction.Isbn,
            UserId = transaction.UserId,
            CardNumber = transaction.CardNumber,
            Timestamp = DateTimeOffset.UtcNow
        };

        var activeBorrows = await GetActiveBorrowsCountAsync(transaction.BookId, cancellationToken);
        _dbContext.PublishedEventLogs.Add(CreateEventLog("book.availability.changed", new BookAvailabilityChangedEvent
        {
            BookId = transaction.BookId,
            Isbn = transaction.Isbn,
            AvailableCopies = activeBorrows,
            Timestamp = DateTimeOffset.UtcNow
        }));

        try
        {
            _dbContext.BorrowTransactions.AddRange(transactions);
            _dbContext.PublishedEventLogs.Add(CreateEventLog("borrow.requested", borrowedEvent));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(500, new { Message = "Failed to save borrow transaction", Details = ex.InnerException?.Message ?? ex.Message });
        }

        return Ok(new BorrowResponse
        {
            TransactionId = transaction.Id,
            BookId = transaction.BookId,
            Isbn = transaction.Isbn,
            UserId = transaction.UserId,
            CardNumber = transaction.CardNumber,
            BorrowedAt = transaction.BorrowedAt,
            DueAt = transaction.DueAt
        });
    }

    [HttpPost("transactions/{id:guid}/return")]
    public async Task<ActionResult<ReturnResponse>> ReturnByTransactionAsync(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.BorrowTransactions
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (transaction is null)
        {
            return NotFound(new { Message = "Borrow transaction not found." });
        }

        if (transaction.ReturnedAt is not null || transaction.Status == "Returned")
        {
            return BadRequest(new { Message = "Borrow transaction was already returned." });
        }

        return await RequestReturnAsync(transaction, cancellationToken);
    }

    [HttpPost("return")]
    public async Task<ActionResult<ReturnResponse>> ReturnAsync([FromBody] ReturnRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateReference(request.BookId, request.Isbn, request.UserId, request.CardNumber);
        if (validationError is not null)
        {
            return BadRequest(new { Message = validationError });
        }

        var transaction = await _dbContext.BorrowTransactions
            .OrderByDescending(item => item.BorrowedAt)
            .FirstOrDefaultAsync(item =>
                item.BookId == request.BookId &&
                item.CardNumber == request.CardNumber &&
                item.ReturnedAt == null,
                cancellationToken);

        if (transaction is null)
        {
            return NotFound(new { Message = "Borrow transaction not found for the given book and reader reference." });
        }

        return await RequestReturnAsync(transaction, cancellationToken);
    }

    [HttpPost("transactions/{id:guid}/return/approve")]
    [HttpPost("transactions/{id:guid}/confirm-return")]
    public async Task<ActionResult<ReturnResponse>> ApproveReturnAsync(
        Guid id,
        [FromBody] ReturnApprovalRequest? request,
        CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.BorrowTransactions
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (transaction is null)
        {
            return NotFound(new { Message = "Borrow transaction not found." });
        }

        if (transaction.Status != "ReturnPending")
        {
            return BadRequest(new { Message = "Transaction is not waiting for return approval." });
        }

        var condition = string.IsNullOrWhiteSpace(request?.Condition) ? "Good" : request.Condition.Trim();
        var conditionNote = string.IsNullOrWhiteSpace(request?.ConditionNote) ? null : request.ConditionNote.Trim();

        return await ReturnTransactionAsync(transaction, DateTime.UtcNow, cancellationToken, condition, conditionNote);
    }

    [HttpPost("transactions/{id:guid}/return/reject")]
    public async Task<ActionResult> RejectReturnAsync(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.BorrowTransactions
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (transaction is null)
        {
            return NotFound(new { Message = "Borrow transaction not found." });
        }

        if (transaction.Status != "ReturnPending")
        {
            return BadRequest(new { Message = "Transaction is not waiting for return approval." });
        }

        transaction.Status = transaction.DueAt < DateTime.UtcNow ? "Overdue" : "Borrowed";
        _dbContext.PublishedEventLogs.Add(CreateEventLog("return.rejected", new
        {
            TransactionId = transaction.Id,
            transaction.BookId,
            transaction.CardNumber,
            RejectedAt = DateTimeOffset.UtcNow
        }));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { Message = "Return request rejected.", TransactionId = transaction.Id });
    }

    private async Task<ActionResult<ReturnResponse>> RequestReturnAsync(
        BorrowTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (transaction.Status == "Pending")
        {
            return BadRequest(new { Message = "Borrow request is still waiting for approval." });
        }

        if (transaction.Status == "ReturnPending")
        {
            return Accepted(new
            {
                Message = "Return request is already waiting for librarian approval.",
                TransactionId = transaction.Id,
                Status = transaction.Status
            });
        }

        transaction.Status = "ReturnPending";
        _dbContext.PublishedEventLogs.Add(CreateEventLog("return.requested", new
        {
            TransactionId = transaction.Id,
            transaction.BookId,
            transaction.CardNumber,
            RequestedAt = DateTimeOffset.UtcNow
        }));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Accepted(new
        {
            Message = "Return request is waiting for librarian approval.",
            TransactionId = transaction.Id,
            Status = transaction.Status
        });
    }

    private async Task<ActionResult<ReturnResponse>> ReturnTransactionAsync(
        BorrowTransaction transaction,
        DateTime returnedAt,
        CancellationToken cancellationToken,
        string condition = "Good",
        string? conditionNote = null)
    {
        var overdueDays = Math.Max(0, (int)Math.Ceiling((returnedAt - transaction.DueAt).TotalDays));
        var fineAmount = overdueDays * DailyFineRate;

        // Gá»i API N1 Ä‘á»ƒ cáº­p nháº­t sá»‘ lÆ°á»£ng sÃ¡ch
        var catalogReturnResult = await UpdateCatalogBookReturnAsync(transaction.BookId, 1, cancellationToken);
        if (!catalogReturnResult.IsSuccess)
        {
            return StatusCode(catalogReturnResult.StatusCode, new { Message = catalogReturnResult.Message });
        }

        transaction.ReturnedAt = returnedAt;
        transaction.FineAmount = fineAmount;
        transaction.Status = "Returned";

        if (fineAmount > 0)
        {
            _dbContext.FineCharges.Add(new FineCharge
            {
                Id = Guid.NewGuid(),
                BorrowTransactionId = transaction.Id,
                UserId = transaction.UserId ?? string.Empty,
                CardNumber = transaction.CardNumber,
                Amount = fineAmount,
                Reason = $"Overdue return by {overdueDays} day(s)",
                CreatedAt = returnedAt,
                PaidAt = null
            });
        }

        var returnedEvent = new BookReturnedEvent
        {
            BookId = transaction.BookId,
            Isbn = transaction.Isbn,
            UserId = transaction.UserId,
            CardNumber = transaction.CardNumber,
            Timestamp = DateTimeOffset.UtcNow,
            FineAmount = fineAmount
        };

        var activeBorrows = await GetActiveBorrowsCountAsync(transaction.BookId, cancellationToken);
        _dbContext.PublishedEventLogs.Add(CreateEventLog("book.availability.changed", new BookAvailabilityChangedEvent
        {
            BookId = transaction.BookId,
            Isbn = transaction.Isbn,
            AvailableCopies = activeBorrows,
            Timestamp = DateTimeOffset.UtcNow
        }));

        _dbContext.PublishedEventLogs.Add(CreateEventLog("book.returned", returnedEvent));
        _dbContext.PublishedEventLogs.Add(CreateEventLog("return.condition.checked", new
        {
            TransactionId = transaction.Id,
            transaction.BookId,
            transaction.CardNumber,
            Condition = condition,
            ConditionNote = conditionNote,
            CheckedAt = DateTimeOffset.UtcNow
        }));
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(500, new { Message = "Failed to save return transaction", Details = ex.InnerException?.Message ?? ex.Message });
        }

        return Ok(new ReturnResponse
        {
            TransactionId = transaction.Id,
            BookId = transaction.BookId,
            Isbn = transaction.Isbn,
            UserId = transaction.UserId,
            CardNumber = transaction.CardNumber,
            BorrowedAt = transaction.BorrowedAt,
            ReturnedAt = returnedAt,
            FineAmount = fineAmount
        });
    }

    /// <summary>
    /// Returns all borrow transactions (for reader / admin dashboards).
    /// </summary>
    [HttpGet("transactions")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetTransactionsAsync(
        [FromQuery] string? userId,
        [FromQuery] string? cardNumber,
        [FromQuery] bool? activeOnly,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.BorrowTransactions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(t => t.UserId == userId);
        if (!string.IsNullOrWhiteSpace(cardNumber))
            query = query.Where(t => t.CardNumber == cardNumber);
        if (activeOnly == true)
            query = query.Where(t => t.ReturnedAt == null && t.Status != "Pending");

        var limit = Math.Clamp(pageSize ?? 100, 1, 200);
        var result = await query
            .OrderByDescending(t => t.BorrowedAt)
            .Take(limit)
            .Select(t => new {
                t.Id,
                t.BookId,
                t.Isbn,
                t.UserId,
                t.CardNumber,
                BorrowedAt = t.BorrowedAt,
                DueAt = t.DueAt,
                ReturnedAt = t.ReturnedAt,
                FineAmount = t.FineAmount,
                Status = t.Status == "Pending" ? "Pending" :
                         t.Status == "ReturnPending" ? "ReturnPending" :
                         t.ReturnedAt != null || t.Status == "Returned" ? "Returned" :
                         t.DueAt < DateTime.UtcNow ? "Overdue" : "Borrowed"
            })
            .ToListAsync(cancellationToken);

        // Enrich with book metadata from Catalog API
        var bookIds = result.Select(r => r.BookId).Distinct().ToList();
        var books = await GetBooksFromCatalogAsync(bookIds, cancellationToken);

        var enriched = result.Select(r =>
        {
            books.TryGetValue(r.BookId, out var book);
            return (object)new {
                r.Id,
                r.BookId,
                Isbn = r.Isbn ?? book?.Isbn ?? "",
                r.UserId,
                r.CardNumber,
                r.BorrowedAt,
                r.DueAt,
                r.ReturnedAt,
                r.FineAmount,
                r.Status,
                TenSach = book?.TenSach ?? "",
                TacGia = book?.TacGia ?? "",
                NhaSanXuat = book?.NhaSanXuat ?? "",
                ImageUrl = book?.ImageUrl ?? ""
            };
        }).ToList();

        return Ok(enriched);
    }

    [HttpGet("stats/monthly")]
    public async Task<ActionResult<IReadOnlyList<MonthlyCirculationStatsDto>>> GetMonthlyStatsAsync(CancellationToken cancellationToken)
    {
        var groupedStats = await _dbContext.BorrowTransactions
            .AsNoTracking()
            .GroupBy(item => new { item.BorrowedAt.Year, item.BorrowedAt.Month })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                BorrowCount = group.Count(),
                ReturnCount = group.Count(item => item.ReturnedAt != null)
            })
            .OrderBy(item => item.Year)
            .ThenBy(item => item.Month)
            .ToListAsync(cancellationToken);

        var stats = groupedStats
            .Select(item => new MonthlyCirculationStatsDto
            {
                Month = $"ThÃ¡ng {item.Month:00}/{item.Year}",
                BorrowCount = item.BorrowCount,
                ReturnCount = item.ReturnCount
            })
            .ToList();

        return Ok(stats);
    }

    [HttpGet("stats/popular")]
    public async Task<ActionResult<IReadOnlyList<PopularBookStatsDto>>> GetPopularBooksAsync(
        [FromQuery] int top = 10,
        CancellationToken cancellationToken = default)
    {
        var bookIds = await _dbContext.BorrowTransactions
            .AsNoTracking()
            .GroupBy(item => item.BookId)
            .Select(g => g.Key)
            .Take(top * 2)
            .ToListAsync(cancellationToken);

        // Get book info from Catalog API
        var allBooks = await GetBooksFromCatalogAsync(bookIds, cancellationToken);

        var popularBooks = await _dbContext.BorrowTransactions
            .AsNoTracking()
            .GroupBy(item => item.BookId)
            .Select(group => new { BookId = group.Key, BorrowCount = group.Count() })
            .OrderByDescending(item => item.BorrowCount)
            .Take(top)
            .ToListAsync(cancellationToken);

        var result = popularBooks.Select(p => new PopularBookStatsDto
        {
            BookId = p.BookId,
            Title = allBooks.TryGetValue(p.BookId, out var b) ? b.TenSach : p.BookId,
            Author = allBooks.TryGetValue(p.BookId, out var b2) ? b2.TacGia : null,
            BorrowCount = p.BorrowCount
        }).ToList();

        return Ok(result);
    }

    [HttpGet("dashboard-stats")]
    [HttpGet("/api/reports/dashboard")]
    public async Task<ActionResult> GetDashboardStatsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-5);
        var monthKeys = Enumerable.Range(0, 6)
            .Select(offset => monthStart.AddMonths(offset))
            .ToList();

        var totalBorrows = await _dbContext.BorrowTransactions
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var totalReturns = await _dbContext.BorrowTransactions
            .AsNoTracking()
            .CountAsync(item => item.ReturnedAt != null, cancellationToken);

        var activeBorrows = await _dbContext.BorrowTransactions
            .AsNoTracking()
            .CountAsync(item => item.ReturnedAt == null && item.Status != "Pending", cancellationToken);

        var overdueBorrows = await _dbContext.BorrowTransactions
            .AsNoTracking()
            .CountAsync(item => item.ReturnedAt == null && item.Status != "Pending" && item.DueAt < now, cancellationToken);

        var totalFines = await _dbContext.FineCharges
            .AsNoTracking()
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var unpaidFines = await _dbContext.FineCharges
            .AsNoTracking()
            .Where(item => item.PaidAt == null)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var topBooksRaw = await _dbContext.BorrowTransactions
            .AsNoTracking()
            .GroupBy(item => item.BookId)
            .Select(group => new { BookId = group.Key, BorrowCount = group.Count() })
            .OrderByDescending(item => item.BorrowCount)
            .ThenBy(item => item.BookId)
            .Take(5)
            .ToListAsync(cancellationToken);

        var booksById = await GetBooksFromCatalogAsync(topBooksRaw.Select(item => item.BookId).ToList(), cancellationToken);
        var maxBorrowCount = topBooksRaw.Count > 0 ? topBooksRaw.Max(item => item.BorrowCount) : 0;

        var topBorrowedBooks = topBooksRaw.Select(item =>
        {
            booksById.TryGetValue(item.BookId, out var book);
            return new
            {
                bookId = item.BookId,
                bookName = string.IsNullOrWhiteSpace(book?.TenSach) ? item.BookId : book.TenSach,
                borrowCount = item.BorrowCount,
                percentage = maxBorrowCount == 0 ? 0 : (int)Math.Round(item.BorrowCount * 100m / maxBorrowCount)
            };
        }).ToList();

        var borrowTrendRaw = await _dbContext.BorrowTransactions
            .AsNoTracking()
            .Where(item => item.BorrowedAt >= monthStart)
            .GroupBy(item => new { item.BorrowedAt.Year, item.BorrowedAt.Month })
            .Select(group => new { group.Key.Year, group.Key.Month, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var returnTrendRaw = await _dbContext.BorrowTransactions
            .AsNoTracking()
            .Where(item => item.ReturnedAt != null && item.ReturnedAt >= monthStart)
            .GroupBy(item => new { item.ReturnedAt!.Value.Year, item.ReturnedAt.Value.Month })
            .Select(group => new { group.Key.Year, group.Key.Month, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var borrowTrendLookup = borrowTrendRaw.ToDictionary(item => (item.Year, item.Month), item => item.Count);
        var returnTrendLookup = returnTrendRaw.ToDictionary(item => (item.Year, item.Month), item => item.Count);

        var borrowingTrends = monthKeys.Select(month =>
        {
            var key = (month.Year, month.Month);
            return new
            {
                month = month.ToString("MMM", CultureInfo.InvariantCulture),
                borrows = borrowTrendLookup.TryGetValue(key, out var borrowCount) ? borrowCount : 0,
                returns = returnTrendLookup.TryGetValue(key, out var returnCount) ? returnCount : 0
            };
        }).ToList();

        return Ok(new
        {
            totalBorrows,
            totalReturns,
            activeBorrows,
            overdueBorrows,
            totalFines,
            unpaidFines,
            topBorrowedBooks,
            borrowingTrends,
            links = new
            {
                transactions = "/api/circulation/transactions",
                fines = "/api/circulation/fines",
                statistics = "/api/circulation/statistics",
                monthlyStats = "/api/circulation/stats/monthly",
                popularBooks = "/api/circulation/stats/popular"
            }
        });
    }

    private const string DefaultCatalogServiceUrl = "http://localhost:5185";

    private string CatalogServiceUrl =>
        _configuration["CatalogService:BaseUrl"]
        ?? _configuration["CatalogService__BaseUrl"]
        ?? DefaultCatalogServiceUrl;

    [HttpGet("books")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetBooksAsync(
        [FromQuery] string? search,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{CatalogServiceUrl}/api/books";
            if (!string.IsNullOrWhiteSpace(search))
                url = $"{CatalogServiceUrl}/api/books/search?q={Uri.EscapeDataString(search)}";
            
            var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, new { Message = "Catalog service unavailable." });

            var books = await response.Content.ReadFromJsonAsync<List<object>>(cancellationToken);
            if (books != null && limit.HasValue)
                books = books.Take(limit.Value).ToList();
                return Ok(books ?? new List<object>());
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { Message = "Cannot connect to Catalog Service." });
        }
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult> GetUserInfoAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            var identityUrl = _configuration["IdentityService:BaseUrl"] ?? "http://localhost:5001";
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"{identityUrl}/api/User/{userId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return NotFound(new { Message = "User not found." });
            var user = await response.Content.ReadFromJsonAsync<object>(cancellationToken);
            return Ok(user);
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { Message = "Cannot connect to Identity Service." });
        }
    }

    [HttpGet("events")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetPublishedEventsAsync(
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(pageSize ?? 100, 1, 500);
        var events = await _dbContext.PublishedEventLogs
            .AsNoTracking()
            .OrderByDescending(e => e.PublishedAt)
            .Take(limit)
            .Select(e => new { e.Id, e.SourceService, e.EventType, e.PayloadJson, e.PublishedAt })
            .ToListAsync(cancellationToken);

        return Ok(events);
    }

    [HttpGet("statistics")]
    public async Task<ActionResult> GetStatisticsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        var totalCount = await _dbContext.BorrowTransactions.CountAsync(cancellationToken);
        var todayBorrowCount = await _dbContext.BorrowTransactions
            .CountAsync(b => b.BorrowedAt >= today, cancellationToken);
        var totalBorrowed = await _dbContext.BorrowTransactions
            .CountAsync(b => b.ReturnedAt == null && b.Status != "Pending", cancellationToken);
        var totalOverdue = await _dbContext.BorrowTransactions
            .CountAsync(b => b.ReturnedAt == null && b.Status != "Pending" && b.DueAt < now, cancellationToken);
        var totalFines = await _dbContext.FineCharges.SumAsync(f => f.Amount, cancellationToken);
        var totalUsers = await _dbContext.Users.CountAsync(cancellationToken);

        return Ok(new
        {
            totalCount,
            todayBorrowCount,
            totalBorrowed,
            totalOverdue,
            totalFines,
            totalUsers
        });
    }

    [HttpGet("fines")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetFinesAsync(CancellationToken cancellationToken)
    {
        var fines = await _dbContext.FineCharges
            .AsNoTracking()
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new { f.Id, f.BorrowTransactionId, f.UserId, f.CardNumber, f.Amount, f.Reason, f.CreatedAt, f.PaidAt })
            .ToListAsync(cancellationToken);

        return Ok(fines);
    }

    [HttpPost("fines/{id}/pay")]
    public async Task<ActionResult> MarkFinePaidAsync(Guid id, CancellationToken cancellationToken)
    {
        var fine = await _dbContext.FineCharges.FindAsync(new object[] { id }, cancellationToken);
        if (fine is null)
            return NotFound(new { Message = "Fine not found." });

        fine.PaidAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { Message = "Fine marked as paid.", FineId = id });
    }

    [HttpPost("books/{bookId}/reviews")]
    public async Task<ActionResult> ReviewBookAsync(
        string bookId,
        [FromBody] BookReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Rating < 0 || request.Rating > 5)
        {
            return BadRequest(new { Message = "Rating must be between 0 and 5." });
        }

        if (string.IsNullOrWhiteSpace(request.UserId) && string.IsNullOrWhiteSpace(request.CardNumber))
        {
            return BadRequest(new { Message = "Either UserId or CardNumber must be provided." });
        }

        var requestedBookId = bookId.Trim();
        var requestedUserId = request.UserId?.Trim();
        var requestedCardNumber = request.CardNumber?.Trim();
        var existingReviewPayloads = await _dbContext.PublishedEventLogs
            .AsNoTracking()
            .Where(item => item.EventType == "book.reviewed")
            .Select(item => item.PayloadJson)
            .ToListAsync(cancellationToken);

        var alreadyReviewed = existingReviewPayloads
            .Select(TryReadReview)
            .Where(item => item is not null)
            .Select(item => item!)
            .Any(item =>
                string.Equals(item.BookId, requestedBookId, StringComparison.OrdinalIgnoreCase) &&
                (
                    (!string.IsNullOrWhiteSpace(requestedCardNumber) &&
                     string.Equals(item.CardNumber, requestedCardNumber, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(requestedUserId) &&
                     string.Equals(item.UserId, requestedUserId, StringComparison.OrdinalIgnoreCase))
                ));

        if (alreadyReviewed)
        {
            return Conflict(new { Message = "This reader has already reviewed this book." });
        }

        var review = new
        {
            ReviewId = Guid.NewGuid(),
            BookId = requestedBookId,
            UserId = requestedUserId,
            CardNumber = requestedCardNumber,
            Rating = request.Rating,
            Comment = request.Comment?.Trim() ?? string.Empty,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.PublishedEventLogs.Add(CreateEventLog("book.reviewed", review));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(review);
    }

    [HttpGet("books/reviews")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetBookReviewsAsync(
        [FromQuery] string? bookId,
        CancellationToken cancellationToken)
    {
        var logs = await _dbContext.PublishedEventLogs
            .AsNoTracking()
            .Where(item => item.EventType == "book.reviewed")
            .OrderByDescending(item => item.PublishedAt)
            .Select(item => item.PayloadJson)
            .ToListAsync(cancellationToken);

        var reviews = logs
            .Select(TryReadReview)
            .Where(item => item is not null)
            .Select(item => item!)
            .Where(item => string.IsNullOrWhiteSpace(bookId) || item.BookId == bookId)
            .ToList();

        var grouped = reviews
            .GroupBy(item => item.BookId)
            .Select(group => new
            {
                bookId = group.Key,
                averageRating = group.Any() ? Math.Round(group.Average(item => item.Rating), 1) : 0,
                reviewCount = group.Count(),
                reviews = group.Select(item => new
                {
                    item.ReviewId,
                    item.UserId,
                    item.CardNumber,
                    item.Rating,
                    item.Comment,
                    item.CreatedAt
                }).ToList()
            })
            .ToList<object>();

        return Ok(grouped);
    }

    [HttpPost("transactions/{id}/approve")]
    public async Task<ActionResult> ApproveTransactionAsync(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.BorrowTransactions.FindAsync(new object[] { id }, cancellationToken);
        if (transaction is null)
            return NotFound(new { Message = "Transaction not found." });

        if (transaction.Status != "Pending")
            return BadRequest(new { Message = "Only pending borrow requests can be approved." });

        var catalogBorrowResult = await UpdateCatalogBookBorrowAsync(transaction.BookId, 1, cancellationToken);
        if (!catalogBorrowResult.IsSuccess)
            return StatusCode(catalogBorrowResult.StatusCode, new { Message = catalogBorrowResult.Message });

        transaction.Status = "Borrowed";
        _dbContext.PublishedEventLogs.Add(CreateEventLog("transaction.approved", new { TransactionId = id, ApprovedAt = DateTimeOffset.UtcNow }));
        _dbContext.PublishedEventLogs.Add(CreateEventLog("book.borrowed", new BookBorrowedEvent
        {
            BookId = transaction.BookId,
            Isbn = transaction.Isbn,
            UserId = transaction.UserId,
            CardNumber = transaction.CardNumber,
            Timestamp = DateTimeOffset.UtcNow
        }));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { Message = "Transaction approved.", TransactionId = id });
    }

    [HttpPost("transactions/{id}/reject")]
    public async Task<ActionResult> RejectTransactionAsync(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.BorrowTransactions.FindAsync(new object[] { id }, cancellationToken);
        if (transaction is null)
            return NotFound(new { Message = "Transaction not found." });

        if (transaction.Status != "Pending")
            return BadRequest(new { Message = "Only pending borrow requests can be rejected." });

        _dbContext.BorrowTransactions.Remove(transaction);
        _dbContext.PublishedEventLogs.Add(CreateEventLog("transaction.rejected", new { TransactionId = id, RejectedAt = DateTimeOffset.UtcNow }));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { Message = "Transaction rejected.", TransactionId = id });
    }

    [HttpGet("overdue")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetOverdueAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var overdue = await _dbContext.BorrowTransactions
            .AsNoTracking()
            .Where(b => b.ReturnedAt == null && b.Status != "Pending" && b.DueAt < now)
            .OrderBy(b => b.DueAt)
            .Select(b => new { b.Id, b.BookId, b.Isbn, b.UserId, b.CardNumber, b.BorrowedAt, b.DueAt })
            .ToListAsync(cancellationToken);

        return Ok(overdue);
    }

    private static string? ValidateReference(string bookId, string? isbn, string? userId, string? cardNumber)
    {
        if (string.IsNullOrWhiteSpace(bookId) && string.IsNullOrWhiteSpace(isbn))
        {
            return "Either BookId or ISBN must be provided.";
        }

        if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(cardNumber))
        {
            return "Either UserId or CardNumber must be provided.";
        }

        return null;
    }

    private static PublishedEventLog CreateEventLog<T>(string eventType, T payload)
    {
        return new PublishedEventLog
        {
            Id = Guid.NewGuid(),
            SourceService = "N2.Circulation.Api",
            EventType = eventType,
            PayloadJson = JsonSerializer.Serialize(payload),
            PublishedAt = DateTime.UtcNow
        };
    }

    private async Task<int> GetActiveBorrowsCountAsync(string bookId, CancellationToken cancellationToken)
    {
        return await _dbContext.BorrowTransactions
            .CountAsync(item =>
                item.BookId == bookId &&
                item.ReturnedAt == null &&
                item.Status != "Pending",
                cancellationToken);
    }

    private async Task<CatalogUpdateResult> UpdateCatalogBookBorrowAsync(string bookId, int quantity, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            using var content = CreateQuantityContent(quantity);
            using var response = await client.PostAsync($"{CatalogServiceUrl}/api/books/{bookId}/borrow", content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return CatalogUpdateResult.Ok();
            }

            var message = await ReadCatalogErrorAsync(response, cancellationToken);
            return CatalogUpdateResult.Fail((int)response.StatusCode, $"Catalog service rejected borrow for book {bookId}. {message}");
        }
        catch (HttpRequestException ex)
        {
            return CatalogUpdateResult.Fail(503, $"Cannot connect to Catalog Service when borrowing book {bookId}. {ex.Message}");
        }
    }

    private async Task<CatalogUpdateResult> UpdateCatalogBookReturnAsync(string bookId, int quantity, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            using var content = CreateQuantityContent(quantity);
            using var response = await client.PostAsync($"{CatalogServiceUrl}/api/books/{bookId}/return", content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return CatalogUpdateResult.Ok();
            }

            var message = await ReadCatalogErrorAsync(response, cancellationToken);
            return CatalogUpdateResult.Fail((int)response.StatusCode, $"Catalog service rejected return for book {bookId}. {message}");
        }
        catch (HttpRequestException ex)
        {
            return CatalogUpdateResult.Fail(503, $"Cannot connect to Catalog Service when returning book {bookId}. {ex.Message}");
        }
    }

    private static StringContent CreateQuantityContent(int quantity)
    {
        return new StringContent(JsonSerializer.Serialize(new { quantity }), Encoding.UTF8, "application/json");
    }

    private static async Task<string> ReadCatalogErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"Status code: {(int)response.StatusCode}.";
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? body;
            }

            if (json.RootElement.TryGetProperty("Message", out var capitalMessage))
            {
                return capitalMessage.GetString() ?? body;
            }

            if (json.RootElement.TryGetProperty("title", out var title))
            {
                return title.GetString() ?? body;
            }
        }
        catch (JsonException)
        {
            return body;
        }

        return body;
    }

    private async Task<int?> GetBookIdByIsbnAsync(string isbn, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"{CatalogServiceUrl}/api/books/search?q={Uri.EscapeDataString(isbn)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            var books = await response.Content.ReadFromJsonAsync<List<System.Text.Json.JsonElement>>(cancellationToken);
            var book = books?.FirstOrDefault(b => b.TryGetProperty("isbn", out var i) && i.GetString() == isbn);
            if (book is null || !book.Value.TryGetProperty("id", out var idProp))
                return null;
            return idProp.GetInt32();
        }
        catch
        {
            return null;
        }
    }

    private async Task<Dictionary<string, BookInfo>> GetBooksFromCatalogAsync(List<string> bookIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, BookInfo>();
        try
        {
            var client = _httpClientFactory.CreateClient();
            // Fetch all books from catalog
            var response = await client.GetAsync($"{CatalogServiceUrl}/api/books", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return result;

            var books = await response.Content.ReadFromJsonAsync<List<BookInfo>>(cancellationToken);
            if (books == null)
                return result;

            // Filter to only requested book IDs
            var intBookIds = bookIds
                .Select(id => int.TryParse(id, out var n) ? n : 0)
                .Where(n => n > 0)
                .ToHashSet();

            foreach (var book in books.Where(b => intBookIds.Contains(b.Id)))
            {
                result[book.Id.ToString()] = book;
            }
        }
        catch
        {
            // Return empty dict on error
        }
        return result;
    }

    private sealed class BookInfo
    {
        public int Id { get; set; }
        public string TenSach { get; set; } = "";
        public string TacGia { get; set; } = "";
        public string NhaSanXuat { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string Isbn { get; set; } = "";
    }

    private static ReviewDto? TryReadReview(string payloadJson)
    {
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;
            var bookId = root.TryGetProperty("BookId", out var bookIdElement)
                ? bookIdElement.GetString()
                : root.TryGetProperty("bookId", out var camelBookIdElement)
                    ? camelBookIdElement.GetString()
                    : null;

            if (string.IsNullOrWhiteSpace(bookId))
            {
                return null;
            }

            var rating = root.TryGetProperty("Rating", out var ratingElement)
                ? ratingElement.GetInt32()
                : root.TryGetProperty("rating", out var camelRatingElement)
                    ? camelRatingElement.GetInt32()
                    : 0;

            return new ReviewDto
            {
                ReviewId = root.TryGetProperty("ReviewId", out var reviewIdElement) && reviewIdElement.TryGetGuid(out var reviewId)
                    ? reviewId
                    : Guid.Empty,
                BookId = bookId,
                UserId = root.TryGetProperty("UserId", out var userIdElement) ? userIdElement.GetString() : null,
                CardNumber = root.TryGetProperty("CardNumber", out var cardElement) ? cardElement.GetString() : null,
                Rating = Math.Clamp(rating, 0, 5),
                Comment = root.TryGetProperty("Comment", out var commentElement) ? commentElement.GetString() ?? string.Empty : string.Empty,
                CreatedAt = root.TryGetProperty("CreatedAt", out var createdAtElement) && createdAtElement.TryGetDateTimeOffset(out var createdAt)
                    ? createdAt
                    : DateTimeOffset.MinValue
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class ReviewDto
    {
        public Guid ReviewId { get; init; }
        public string BookId { get; init; } = string.Empty;
        public string? UserId { get; init; }
        public string? CardNumber { get; init; }
        public int Rating { get; init; }
        public string Comment { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; init; }
    }

    private sealed record CatalogUpdateResult(bool IsSuccess, int StatusCode, string Message)
    {
        public static CatalogUpdateResult Ok() => new(true, 200, string.Empty);

        public static CatalogUpdateResult Fail(int statusCode, string message) => new(false, statusCode, message);
    }
}

