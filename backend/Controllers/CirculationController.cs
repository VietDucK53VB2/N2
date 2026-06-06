using System.Net.Http.Json;
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

        // Gọi API N1 để cập nhật số lượng sách
        await UpdateCatalogBookBorrowAsync(resolvedBookId, 1, cancellationToken);

        var transaction = new BorrowTransaction
        {
            Id = Guid.NewGuid(),
            BookId = resolvedBookId,
            Isbn = request.Isbn,
            UserId = request.UserId,
            CardNumber = request.CardNumber,
            BorrowedAt = borrowedAt,
            DueAt = dueAt,
            FineAmount = 0m,
            Status = "Approved" // Auto-approve for now
        };

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
            _dbContext.BorrowTransactions.Add(transaction);
            _dbContext.PublishedEventLogs.Add(CreateEventLog("book.borrowed", borrowedEvent));
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

        var returnedAt = request.ReturnedAt ?? DateTime.UtcNow;
        var overdueDays = Math.Max(0, (int)Math.Ceiling((returnedAt - transaction.DueAt).TotalDays));
        var fineAmount = overdueDays * DailyFineRate;

        // Gọi API N1 để cập nhật số lượng sách
        await UpdateCatalogBookReturnAsync(request.BookId, 1, cancellationToken);

        transaction.ReturnedAt = returnedAt;
        transaction.FineAmount = fineAmount;

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
            query = query.Where(t => t.ReturnedAt == null);

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
                         t.ReturnedAt != null ? "Returned" :
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
                Month = $"Tháng {item.Month:00}/{item.Year}",
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
            .CountAsync(b => b.ReturnedAt == null, cancellationToken);
        var totalOverdue = await _dbContext.BorrowTransactions
            .CountAsync(b => b.ReturnedAt == null && b.DueAt < now, cancellationToken);
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

    [HttpPost("transactions/{id}/approve")]
    public async Task<ActionResult> ApproveTransactionAsync(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.BorrowTransactions.FindAsync(new object[] { id }, cancellationToken);
        if (transaction is null)
            return NotFound(new { Message = "Transaction not found." });

        transaction.Status = "Approved";
        _dbContext.PublishedEventLogs.Add(CreateEventLog("transaction.approved", new { TransactionId = id, ApprovedAt = DateTimeOffset.UtcNow }));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { Message = "Transaction approved.", TransactionId = id });
    }

    [HttpPost("transactions/{id}/reject")]
    public async Task<ActionResult> RejectTransactionAsync(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.BorrowTransactions.FindAsync(new object[] { id }, cancellationToken);
        if (transaction is null)
            return NotFound(new { Message = "Transaction not found." });

        // Remove the transaction
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
            .Where(b => b.ReturnedAt == null && b.DueAt < now)
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
            .CountAsync(item => item.BookId == bookId && item.ReturnedAt == null, cancellationToken);
    }

    private async Task UpdateCatalogBookBorrowAsync(string bookId, int quantity, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var content = new StringContent($"{{\"quantity\":{quantity}}}", Encoding.UTF8, "application/json");
            await client.PostAsync($"{CatalogServiceUrl}/api/books/{bookId}/borrow", content, cancellationToken);
        }
        catch
        {
            // Log error but don't fail the transaction
        }
    }

    private async Task UpdateCatalogBookReturnAsync(string bookId, int quantity, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var content = new StringContent($"{{\"quantity\":{quantity}}}", Encoding.UTF8, "application/json");
            await client.PostAsync($"{CatalogServiceUrl}/api/books/{bookId}/return", content, cancellationToken);
        }
        catch
        {
            // Log error but don't fail the transaction
        }
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
}
