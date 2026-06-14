using System.Net.Http.Json;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using N2.Circulation.Api.Contracts;
using N2.Circulation.Api.Data;
using N2.Circulation.Api.Models;

namespace N2.Circulation.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/circulation")]
public sealed class CirculationController : ControllerBase
{
    private const decimal DailyFineRate = 5000m;
    private const int DefaultBorrowDays = 14;
    private const int DefaultMonthlyBorrowLimit = 5;
    private const string BorrowPolicyEventType = "BorrowPolicyUpdated";
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
        if (!await CanAccessCardNumberAsync(request.CardNumber, cancellationToken))
        {
            return Forbid();
        }

        var validationError = ValidateReference(request.BookId, request.Isbn, request.UserId, request.CardNumber);
        if (validationError is not null)
        {
            return BadRequest(new { Message = validationError });
        }

        var borrowedAt = NormalizeUtc(request.BorrowedAt ?? DateTime.UtcNow);
        var dueAt = NormalizeUtc(request.DueAt ?? borrowedAt.AddDays(DefaultBorrowDays));
        var quantity = Math.Clamp(request.Quantity, 1, 20);

        if (dueAt <= DateTime.UtcNow)
        {
            return BadRequest(new { Message = "Due date must be in the future." });
        }

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

        var monthlyLimit = await GetMonthlyBorrowLimitAsync(cancellationToken);
        if (monthlyLimit > 0 && !string.IsNullOrWhiteSpace(request.CardNumber))
        {
            var activeBorrowCount = await _dbContext.BorrowTransactions
                .AsNoTracking()
                .CountAsync(item =>
                    item.CardNumber == request.CardNumber &&
                    item.ReturnedAt == null &&
                    item.Status != "Returned",
                    cancellationToken);

            if (activeBorrowCount + quantity > monthlyLimit)
            {
                return BadRequest(new
                {
                    Message = $"Bạn đã đạt giới hạn mượn. Mỗi độc giả chỉ được giữ tối đa {monthlyLimit} cuốn đang mượn hoặc chờ xử lý. Sách đã trả sẽ không tính vào giới hạn.",
                    MonthlyBorrowLimit = monthlyLimit,
                    ActiveBorrowCount = activeBorrowCount,
                    RequestedQuantity = quantity
                });
            }
        }

        var transactions = Enumerable.Range(0, quantity)
            .Select(_ => new BorrowTransaction
            {
                Id = Guid.NewGuid(),
                BookId = resolvedBookId,
                Isbn = request.Isbn,
                UserId = request.UserId,
                CardNumber = request.CardNumber,
                ReaderName = string.IsNullOrWhiteSpace(request.ReaderName) ? null : request.ReaderName.Trim(),
                ReaderUsername = string.IsNullOrWhiteSpace(request.ReaderUsername) ? null : request.ReaderUsername.Trim(),
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
            ReaderName = transaction.ReaderName,
            ReaderUsername = transaction.ReaderUsername,
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
            ReaderName = transaction.ReaderName,
            ReaderUsername = transaction.ReaderUsername,
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

        if (!await CanAccessCardNumberAsync(transaction.CardNumber, cancellationToken))
        {
            return Forbid();
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
        if (!await CanAccessCardNumberAsync(request.CardNumber, cancellationToken))
        {
            return Forbid();
        }

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
    [Authorize(Roles = "Librarian,Admin,librarian,admin")]
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

        var condition = NormalizeReturnCondition(request?.Condition);
        var conditionNote = string.IsNullOrWhiteSpace(request?.ConditionNote) ? null : request.ConditionNote.Trim();

        return await ReturnTransactionAsync(transaction, DateTime.UtcNow, cancellationToken, condition, conditionNote);
    }

    [HttpPost("transactions/{id:guid}/return/reject")]
    [Authorize(Roles = "Librarian,Admin,librarian,admin")]
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

        var shouldReturnCopyToCatalog = ShouldReturnCopyToCatalog(condition);
        if (shouldReturnCopyToCatalog)
        {
            var catalogReturnResult = await UpdateCatalogBookReturnAsync(transaction.BookId, 1, cancellationToken);
            if (!catalogReturnResult.IsSuccess)
            {
                return StatusCode(catalogReturnResult.StatusCode, new { Message = catalogReturnResult.Message });
            }
        }
        else
        {
            var catalogWriteOffResult = await WriteOffCatalogBookAsync(
                transaction.BookId,
                1,
                condition == "Lost" ? "Lost" : "HeavyDamage",
                conditionNote,
                cancellationToken);
            if (!catalogWriteOffResult.IsSuccess)
            {
                return StatusCode(catalogWriteOffResult.StatusCode, new { Message = catalogWriteOffResult.Message });
            }
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
            ReturnedToCatalog = shouldReturnCopyToCatalog,
            InventoryAction = shouldReturnCopyToCatalog ? "ReturnedToShelf" : "RemovedFromCirculation",
            CheckedAt = DateTimeOffset.UtcNow
        }));

        if (!shouldReturnCopyToCatalog)
        {
            _dbContext.PublishedEventLogs.Add(CreateEventLog("book.copy.removed", new
            {
                TransactionId = transaction.Id,
                transaction.BookId,
                transaction.CardNumber,
                Condition = condition,
                ConditionNote = conditionNote,
                RemovedAt = DateTimeOffset.UtcNow,
                Reason = condition == "Lost" ? "Lost" : "HeavyDamage"
            }));
        }

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
            FineAmount = fineAmount,
            Condition = condition,
            ReturnedToCatalog = shouldReturnCopyToCatalog
        });
    }

    /// <summary>
    /// Returns all borrow transactions (for reader / admin dashboards).
    /// </summary>
    [HttpGet("transactions")]
    [HttpGet("/api/transactions")]
    [Authorize(Roles = "Reader,Librarian,Admin,reader,librarian,admin")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetTransactionsAsync(
        [FromQuery] string? userId,
        [FromQuery] string? cardNumber,
        [FromQuery] bool? activeOnly,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var requestedCardNumber = cardNumber;
        if (!await CanAccessCardNumberAsync(requestedCardNumber, cancellationToken))
        {
            return Forbid();
        }

        var query = _dbContext.BorrowTransactions.AsNoTracking();

        if (!IsStaffUser())
        {
            var tokenCardNumber = GetTokenCardNumber();
            cardNumber = string.IsNullOrWhiteSpace(tokenCardNumber) ? requestedCardNumber : tokenCardNumber;
        }

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
                t.ReaderName,
                t.ReaderUsername,
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
        var cardNumbers = result
            .Select(r => r.CardNumber)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var userRefs = result
            .Select(r => r.UserId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var userGuidRefs = userRefs
            .Where(value => Guid.TryParse(value, out _))
            .Select(value => Guid.Parse(value!))
            .Distinct()
            .ToList();

        var transactionUsers = await _dbContext.Users
            .AsNoTracking()
            .Where(user =>
                (user.CardNumber != null && cardNumbers.Contains(user.CardNumber)) ||
                userGuidRefs.Contains(user.Id) ||
                userRefs.Contains(user.Username))
            .Select(user => new { user.Id, user.Username, user.FullName, user.CardNumber })
            .ToListAsync(cancellationToken);

        var usersByCard = transactionUsers
            .Where(user => !string.IsNullOrWhiteSpace(user.CardNumber))
            .GroupBy(user => user.CardNumber!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var usersById = transactionUsers.ToDictionary(user => user.Id.ToString(), user => user, StringComparer.OrdinalIgnoreCase);
        var usersByUsername = transactionUsers.ToDictionary(user => user.Username, user => user, StringComparer.OrdinalIgnoreCase);
        var cardsNeedingIdentity = result
            .Where(r => string.IsNullOrWhiteSpace(r.ReaderName) &&
                        !string.IsNullOrWhiteSpace(r.CardNumber) &&
                        !usersByCard.ContainsKey(r.CardNumber))
            .Select(r => r.CardNumber!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var identityUsersByCard = await GetUsersFromIdentityByCardAsync(cardsNeedingIdentity, cancellationToken);

        var enriched = result.Select(r =>
        {
            books.TryGetValue(r.BookId, out var book);
            var readerName = r.ReaderName ?? "";
            var readerUsername = r.ReaderUsername ?? "";
            if (!string.IsNullOrWhiteSpace(r.CardNumber) && usersByCard.TryGetValue(r.CardNumber, out var byCard))
            {
                if (string.IsNullOrWhiteSpace(readerName))
                    readerName = string.IsNullOrWhiteSpace(byCard.FullName) ? byCard.Username : byCard.FullName;
                if (string.IsNullOrWhiteSpace(readerUsername))
                    readerUsername = byCard.Username;
            }
            else if (!string.IsNullOrWhiteSpace(r.UserId) && usersById.TryGetValue(r.UserId, out var byId))
            {
                if (string.IsNullOrWhiteSpace(readerName))
                    readerName = string.IsNullOrWhiteSpace(byId.FullName) ? byId.Username : byId.FullName;
                if (string.IsNullOrWhiteSpace(readerUsername))
                    readerUsername = byId.Username;
            }
            else if (!string.IsNullOrWhiteSpace(r.UserId) && usersByUsername.TryGetValue(r.UserId, out var byUsername))
            {
                if (string.IsNullOrWhiteSpace(readerName))
                    readerName = string.IsNullOrWhiteSpace(byUsername.FullName) ? byUsername.Username : byUsername.FullName;
                if (string.IsNullOrWhiteSpace(readerUsername))
                    readerUsername = byUsername.Username;
            }
            else if (!string.IsNullOrWhiteSpace(r.CardNumber) && identityUsersByCard.TryGetValue(r.CardNumber, out var byIdentityCard))
            {
                if (string.IsNullOrWhiteSpace(readerName))
                    readerName = string.IsNullOrWhiteSpace(byIdentityCard.FullName) ? byIdentityCard.Username : byIdentityCard.FullName;
                if (string.IsNullOrWhiteSpace(readerUsername))
                    readerUsername = byIdentityCard.Username;
            }

            return (object)new {
                r.Id,
                r.BookId,
                Isbn = r.Isbn ?? book?.Isbn ?? "",
                r.UserId,
                r.CardNumber,
                ReaderName = string.IsNullOrWhiteSpace(readerName) ? r.CardNumber ?? r.UserId ?? "" : readerName,
                ReaderUsername = readerUsername,
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
    [Authorize(Roles = "Librarian,Admin,librarian,admin")]
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
    [HttpGet("stats/top-borrowed")]
    [Authorize(Roles = "Librarian,Admin,librarian,admin")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetPopularBooksAsync(
        [FromQuery] int top = 10,
        CancellationToken cancellationToken = default)
    {
        var popularBooks = await _dbContext.BorrowTransactions
            .AsNoTracking()
            .GroupBy(item => item.BookId)
            .Select(group => new { BookId = group.Key, BorrowCount = group.Count() })
            .OrderByDescending(item => item.BorrowCount)
            .Take(top)
            .ToListAsync(cancellationToken);

        var allBooks = await GetBooksFromCatalogAsync(popularBooks.Select(item => item.BookId).ToList(), cancellationToken);
        var maxBorrowCount = popularBooks.Count > 0 ? popularBooks.Max(item => item.BorrowCount) : 0;
        var result = popularBooks.Select(p =>
        {
            allBooks.TryGetValue(p.BookId, out var book);
            var title = string.IsNullOrWhiteSpace(book?.TenSach) ? p.BookId : book.TenSach;
            var author = book?.TacGia;
            var percentage = maxBorrowCount == 0 ? 0 : (int)Math.Round(p.BorrowCount * 100m / maxBorrowCount);
            return (object)new
            {
                bookId = p.BookId,
                bookName = title,
                title,
                author,
                borrowCount = p.BorrowCount,
                percentage,
                BookId = p.BookId,
                Title = title,
                Author = author,
                BorrowCount = p.BorrowCount,
                Percentage = percentage
            };
        }).ToList();

        return Ok(result);
    }

    [HttpGet("dashboard-stats")]
    [HttpGet("reports/dashboard")]
    [HttpGet("/api/reports/dashboard")]
    [Authorize(Roles = "Librarian,Admin,librarian,admin")]
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

    private string IdentityServiceUrl =>
        _configuration["IdentityService:BaseUrl"]
        ?? _configuration["IdentityService__BaseUrl"]
        ?? "http://localhost:5001";

    private string? IdentityUserByCardPathTemplate =>
        _configuration["IdentityService:UserByCardPathTemplate"]
        ?? _configuration["IdentityService__UserByCardPathTemplate"];

    [HttpGet("books")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetBooksAsync(
        [FromQuery] string? search,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            ForwardAuthorizationHeader(client);
            var url = $"{CatalogServiceUrl}/api/books";
            if (!string.IsNullOrWhiteSpace(search))
                url = $"{CatalogServiceUrl}/api/books/search?q={Uri.EscapeDataString(search)}";
            
            var response = await client.GetAsync(url, cancellationToken);
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                var internalClient = _httpClientFactory.CreateClient();
                response = await internalClient.GetAsync(url, cancellationToken);
            }
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

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        var paths = new[]
        {
            "/api/categories",
            "/api/book-categories",
            "/api/books/categories",
            "/api/genres",
            "/api/the-loai",
            "/api/TheLoai"
        };

        try
        {
            foreach (var path in paths)
            {
                var client = _httpClientFactory.CreateClient();
                ForwardAuthorizationHeader(client);
                var response = await client.GetAsync($"{CatalogServiceUrl.TrimEnd('/')}{path}", cancellationToken);
                if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                {
                    var internalClient = _httpClientFactory.CreateClient();
                    response = await internalClient.GetAsync($"{CatalogServiceUrl.TrimEnd('/')}{path}", cancellationToken);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    continue;

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, new { Message = "Catalog category service unavailable." });

                var categories = await response.Content.ReadFromJsonAsync<List<object>>(cancellationToken);
                return Ok(categories ?? new List<object>());
            }

            return Ok(Array.Empty<object>());
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
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"{IdentityServiceUrl}/api/User/{userId}", cancellationToken);
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
    [Authorize(Roles = "Librarian,Admin,librarian,admin")]
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
    [Authorize(Roles = "Librarian,Admin,librarian,admin")]
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
    [Authorize(Roles = "Librarian,Admin,librarian,admin")]
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
    [Authorize(Roles = "Librarian,Admin,librarian,admin")]
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

        if (!await CanAccessCardNumberAsync(requestedCardNumber, cancellationToken))
        {
            return Forbid();
        }

        var hasApprovedBorrow = await _dbContext.BorrowTransactions
            .AsNoTracking()
            .AnyAsync(item =>
                item.BookId == requestedBookId &&
                item.Status != "Pending" &&
                (
                    (!string.IsNullOrWhiteSpace(requestedCardNumber) && item.CardNumber == requestedCardNumber) ||
                    (!string.IsNullOrWhiteSpace(requestedUserId) && item.UserId == requestedUserId)
                ),
                cancellationToken);

        if (!hasApprovedBorrow)
        {
            return BadRequest(new { Message = "Book can only be reviewed after the borrow request is approved." });
        }

        var catalogReviews = await GetCatalogBookReviewsAsync(requestedBookId, cancellationToken);
        var catalogAlreadyReviewed = catalogReviews.Any(item =>
            string.Equals(item.BookId, requestedBookId, StringComparison.OrdinalIgnoreCase) &&
            (
                (!string.IsNullOrWhiteSpace(requestedCardNumber) &&
                 string.Equals(item.CardNumber, requestedCardNumber, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(requestedUserId) &&
                 string.Equals(item.UserId, requestedUserId, StringComparison.OrdinalIgnoreCase))
            ));

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

        if (catalogAlreadyReviewed || alreadyReviewed)
        {
            return Conflict(new { Message = "This reader has already reviewed this book." });
        }

        var review = new
        {
            userId = requestedUserId,
            cardNumber = requestedCardNumber,
            username = request.Username?.Trim() ?? GetClaimValue(ClaimTypes.Name) ?? GetClaimValue("username"),
            fullName = request.FullName?.Trim() ?? GetClaimValue("fullName") ?? GetClaimValue(ClaimTypes.Name),
            rating = request.Rating,
            comment = request.Comment?.Trim() ?? string.Empty,
            createdAt = request.CreatedAt ?? DateTimeOffset.UtcNow
        };

        try
        {
            var client = _httpClientFactory.CreateClient();
            ForwardAuthorizationHeader(client);
            using var content = CreateJsonContent(review);
            using var response = await client.PostAsync($"{CatalogServiceUrl}/api/books/{requestedBookId}/reviews", content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _dbContext.PublishedEventLogs.Add(CreateEventLog("book.reviewed", new
                {
                    BookId = requestedBookId,
                    review.userId,
                    review.cardNumber,
                    review.username,
                    review.fullName,
                    review.rating,
                    review.comment,
                    review.createdAt
                }));
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return new ContentResult
            {
                Content = body,
                ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/json",
                StatusCode = (int)response.StatusCode
            };
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(503, new { Message = $"Cannot connect to Catalog Service when reviewing book {requestedBookId}. {ex.Message}" });
        }
    }

    [HttpGet("books/reviews")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetBookReviewsAsync(
        [FromQuery] string? bookId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(bookId))
        {
            var catalogReviews = await GetCatalogBookReviewsAsync(bookId.Trim(), cancellationToken);
            return Ok(GroupReviews(catalogReviews));
        }

        var catalogGrouped = await GetCatalogReviewsFromBooksAsync(cancellationToken);
        if (catalogGrouped.Count > 0)
        {
            return Ok(catalogGrouped);
        }

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

        return Ok(GroupReviews(reviews));
    }

    [HttpPost("transactions/{id}/approve")]
    [Authorize(Roles = "Librarian,Admin,librarian,admin")]
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

        var approvedAt = DateTime.UtcNow;
        var requestedDueAt = NormalizeUtc(transaction.DueAt);
        transaction.Status = "Borrowed";
        transaction.BorrowedAt = approvedAt;
        transaction.DueAt = requestedDueAt > approvedAt ? requestedDueAt : approvedAt.AddDays(DefaultBorrowDays);
        _dbContext.PublishedEventLogs.Add(CreateEventLog("transaction.approved", new { TransactionId = id, ApprovedAt = new DateTimeOffset(approvedAt, TimeSpan.Zero) }));
        _dbContext.PublishedEventLogs.Add(CreateEventLog("book.borrowed", new BookBorrowedEvent
        {
            BookId = transaction.BookId,
            Isbn = transaction.Isbn,
            UserId = transaction.UserId,
            CardNumber = transaction.CardNumber,
            Timestamp = new DateTimeOffset(approvedAt, TimeSpan.Zero)
        }));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { Message = "Transaction approved.", TransactionId = id });
    }

    [HttpGet("settings/borrow-policy")]
    [Authorize(Roles = "Librarian,Admin,librarian,admin")]
    public async Task<ActionResult<object>> GetBorrowPolicyAsync(CancellationToken cancellationToken)
    {
        var monthlyBorrowLimit = await GetMonthlyBorrowLimitAsync(cancellationToken);
        return Ok(new { monthlyBorrowLimit });
    }

    [HttpPut("settings/borrow-policy")]
    [Authorize(Roles = "Librarian,Admin,librarian,admin")]
    public async Task<ActionResult<object>> UpdateBorrowPolicyAsync([FromBody] BorrowPolicyRequest request, CancellationToken cancellationToken)
    {
        var monthlyBorrowLimit = Math.Clamp(request.MonthlyBorrowLimit, 1, 100);
        await SetMonthlyBorrowLimitAsync(monthlyBorrowLimit, cancellationToken);
        return Ok(new { message = "Borrow policy updated.", monthlyBorrowLimit });
    }

    [HttpPost("transactions/{id}/reject")]
    [Authorize(Roles = "Librarian,Admin,librarian,admin")]
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
    [Authorize(Roles = "Librarian,Admin,librarian,admin")]
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

    private bool IsStaffUser()
    {
        return User.IsInRole("Admin") ||
               User.IsInRole("admin") ||
               User.IsInRole("Librarian") ||
               User.IsInRole("librarian");
    }

    private void ForwardAuthorizationHeader(HttpClient client)
    {
        if (Request.Headers.TryGetValue("Authorization", out var authorization) &&
            !string.IsNullOrWhiteSpace(authorization.ToString()))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization.ToString());
        }
    }

    private string? GetClaimValue(string claimType)
    {
        return User.Claims.FirstOrDefault(claim => claim.Type == claimType)?.Value;
    }

    private async Task<bool> CanAccessCardNumberAsync(string? cardNumber, CancellationToken cancellationToken)
    {
        if (IsStaffUser())
        {
            return true;
        }

        var tokenCardNumber = GetTokenCardNumber();
        if (string.IsNullOrWhiteSpace(cardNumber))
        {
            return false;
        }

        var requestedCardNumber = cardNumber.Trim();
        if (!string.IsNullOrWhiteSpace(tokenCardNumber) &&
            string.Equals(requestedCardNumber, tokenCardNumber.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var usersByCard = await GetUsersFromIdentityByCardAsync(new[] { requestedCardNumber }, cancellationToken);
        return usersByCard.TryGetValue(requestedCardNumber, out var identityUser) &&
               TokenMatchesIdentityUser(identityUser);
    }

    private bool TokenMatchesIdentityUser(ReaderIdentityInfo identityUser)
    {
        var tokenUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ??
            User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/nameidentifier") ??
            User.FindFirstValue("sub") ??
            User.FindFirstValue("id") ??
            User.FindFirstValue("userId");
        var tokenUsername =
            User.FindFirstValue(ClaimTypes.Name) ??
            User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name") ??
            User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/name") ??
            User.FindFirstValue("username") ??
            User.FindFirstValue("preferred_username");
        var tokenEmail =
            User.FindFirstValue(ClaimTypes.Email) ??
            User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress") ??
            User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/emailaddress") ??
            User.FindFirstValue("email") ??
            User.FindFirstValue("emailAddress");

        return SameIdentityValue(tokenUserId, identityUser.Id) ||
               SameIdentityValue(tokenUsername, identityUser.Username) ||
               SameIdentityValue(tokenUsername, identityUser.FullName) ||
               SameIdentityValue(tokenEmail, identityUser.Email);
    }

    private static bool SameIdentityValue(string? left, string? right)
    {
        return !string.IsNullOrWhiteSpace(left) &&
               !string.IsNullOrWhiteSpace(right) &&
               string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private string? GetTokenCardNumber()
    {
        return User.FindFirstValue("cardNumber") ??
               User.FindFirstValue("CardNumber") ??
               User.FindFirstValue("libraryCardNumber") ??
               User.FindFirstValue("LibraryCardNumber") ??
               User.FindFirstValue("library_card") ??
               User.FindFirstValue("library_card_number") ??
               User.FindFirstValue("readerCard") ??
               User.FindFirstValue("ReaderCard");
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

    private static string NormalizeReturnCondition(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return "Good";
        }

        var value = condition.Trim();
        if (value.Equals("LightDamage", StringComparison.OrdinalIgnoreCase))
        {
            return "LightDamage";
        }

        if (value.Equals("HeavyDamage", StringComparison.OrdinalIgnoreCase))
        {
            return "HeavyDamage";
        }

        if (value.Equals("Lost", StringComparison.OrdinalIgnoreCase))
        {
            return "Lost";
        }

        return "Good";
    }

    private static bool ShouldReturnCopyToCatalog(string? condition)
    {
        return !string.Equals(condition, "HeavyDamage", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(condition, "Lost", StringComparison.OrdinalIgnoreCase);
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
            ForwardAuthorizationHeader(client);
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
            ForwardAuthorizationHeader(client);
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

    private async Task<CatalogUpdateResult> WriteOffCatalogBookAsync(
        string bookId,
        int quantity,
        string reason,
        string? note,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            ForwardAuthorizationHeader(client);
            using var content = CreateJsonContent(new
            {
                quantity = Math.Max(1, quantity),
                reason,
                note = note ?? string.Empty
            });
            using var response = await client.PostAsync($"{CatalogServiceUrl}/api/books/{bookId}/write-off", content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return CatalogUpdateResult.Ok();
            }

            var message = await ReadCatalogErrorAsync(response, cancellationToken);
            return CatalogUpdateResult.Fail((int)response.StatusCode, $"Catalog service rejected write-off for book {bookId}. {message}");
        }
        catch (HttpRequestException ex)
        {
            return CatalogUpdateResult.Fail(503, $"Cannot connect to Catalog Service when writing off book {bookId}. {ex.Message}");
        }
    }

    private static StringContent CreateQuantityContent(int quantity)
    {
        return new StringContent(JsonSerializer.Serialize(new { quantity }), Encoding.UTF8, "application/json");
    }

    private static StringContent CreateJsonContent<T>(T payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }

    private async Task<List<ReviewDto>> GetCatalogBookReviewsAsync(string bookId, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            ForwardAuthorizationHeader(client);
            using var response = await client.GetAsync($"{CatalogServiceUrl}/api/books/{bookId}/reviews", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new List<ReviewDto>();
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return ReadReviewsFromJson(body, bookId);
        }
        catch
        {
            return new List<ReviewDto>();
        }
    }

    private async Task<List<object>> GetCatalogReviewsFromBooksAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            ForwardAuthorizationHeader(client);
            using var response = await client.GetAsync($"{CatalogServiceUrl}/api/books", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new List<object>();
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new List<object>();
            }

            var groups = new List<object>();
            foreach (var book in document.RootElement.EnumerateArray())
            {
                var id = ReadString(book, "id", "Id", "bookId", "BookId");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var latestReviews = ReadArray(book, "latestReviews", "LatestReviews");
                var reviews = latestReviews.Select(item => TryReadReview(item, id)).Where(item => item is not null).Select(item => item!).ToList();
                var averageRating = ReadDecimal(book, "averageRating", "AverageRating");
                var reviewCount = ReadInt(book, "reviewCount", "ReviewCount");

                if (reviews.Count == 0 && reviewCount <= 0)
                {
                    continue;
                }

                groups.Add(new
                {
                    bookId = id,
                    averageRating = averageRating > 0 ? averageRating : (reviews.Count > 0 ? Math.Round(reviews.Average(item => item.Rating), 1) : 0),
                    reviewCount = reviewCount > 0 ? reviewCount : reviews.Count,
                    reviews = reviews.Select(ToReviewResponse).ToList()
                });
            }

            return groups;
        }
        catch
        {
            return new List<object>();
        }
    }

    private static List<object> GroupReviews(IEnumerable<ReviewDto> reviews)
    {
        return reviews
            .GroupBy(item => item.BookId)
            .Select(group => new
            {
                bookId = group.Key,
                averageRating = group.Any() ? Math.Round(group.Average(item => item.Rating), 1) : 0,
                reviewCount = group.Count(),
                reviews = group.Select(ToReviewResponse).ToList()
            })
            .ToList<object>();
    }

    private static object ToReviewResponse(ReviewDto item)
    {
        return new
        {
            id = item.Id,
            reviewId = item.ReviewId,
            bookId = item.BookId,
            userId = item.UserId,
            cardNumber = item.CardNumber,
            username = item.Username,
            fullName = item.FullName,
            rating = item.Rating,
            comment = item.Comment,
            createdAt = item.CreatedAt
        };
    }

    private static List<ReviewDto> ReadReviewsFromJson(string body, string? fallbackBookId = null)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new List<ReviewDto>();
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                return root.EnumerateArray()
                    .Select(item => TryReadReview(item, fallbackBookId))
                    .Where(item => item is not null)
                    .Select(item => item!)
                    .ToList();
            }

            var nested = ReadArray(root, "reviews", "Reviews", "latestReviews", "LatestReviews");
            if (nested.Count > 0)
            {
                return nested
                    .Select(item => TryReadReview(item, fallbackBookId ?? ReadString(root, "bookId", "BookId", "id", "Id")))
                    .Where(item => item is not null)
                    .Select(item => item!)
                    .ToList();
            }

            var single = TryReadReview(root, fallbackBookId);
            return single is null ? new List<ReviewDto>() : new List<ReviewDto> { single };
        }
        catch
        {
            return new List<ReviewDto>();
        }
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
            ForwardAuthorizationHeader(client);
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
            ForwardAuthorizationHeader(client);
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

    private async Task<Dictionary<string, ReaderIdentityInfo>> GetUsersFromIdentityByCardAsync(
        IReadOnlyCollection<string> cardNumbers,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, ReaderIdentityInfo>(StringComparer.OrdinalIgnoreCase);
        if (cardNumbers.Count == 0)
        {
            return result;
        }

        var templates = new List<string>();
        if (!string.IsNullOrWhiteSpace(IdentityUserByCardPathTemplate))
        {
            templates.Add(IdentityUserByCardPathTemplate);
        }

        templates.AddRange(new[]
        {
            "/api/User/by-card/{cardNumber}",
            "/api/User/card/{cardNumber}",
            "/api/User?cardNumber={cardNumber}",
            "/api/User/search?cardNumber={cardNumber}"
        });

        var client = _httpClientFactory.CreateClient();
        if (Request.Headers.TryGetValue("Authorization", out var authorization))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization.ToString());
        }

        foreach (var cardNumber in cardNumbers)
        {
            foreach (var template in templates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var path = template.Replace("{cardNumber}", Uri.EscapeDataString(cardNumber), StringComparison.OrdinalIgnoreCase);
                var url = path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? path
                    : $"{IdentityServiceUrl.TrimEnd('/')}/{path.TrimStart('/')}";

                try
                {
                    using var response = await client.GetAsync(url, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                    var user = TryReadIdentityUser(document.RootElement, cardNumber);
                    if (user is not null)
                    {
                        result[cardNumber] = user;
                        break;
                    }
                }
                catch
                {
                    // Identity integration is best-effort; missing user names must not break circulation.
                }
            }
        }

        return result;
    }

    private static ReaderIdentityInfo? TryReadIdentityUser(JsonElement root, string fallbackCardNumber)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                var user = TryReadIdentityUser(item, fallbackCardNumber);
                if (user is not null)
                {
                    return user;
                }
            }

            return null;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var userElement = root;
        if (root.TryGetProperty("user", out var nestedUser) && nestedUser.ValueKind == JsonValueKind.Object)
        {
            userElement = nestedUser;
        }
        else if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            userElement = data;
        }

        var cardNumber =
            TryGetString(userElement, "cardNumber") ??
            TryGetString(userElement, "CardNumber") ??
            TryGetString(userElement, "libraryCardNumber") ??
            TryGetString(userElement, "LibraryCardNumber") ??
            TryGetNestedString(userElement, "libraryCard", "cardNumber") ??
            TryGetNestedString(userElement, "LibraryCard", "CardNumber") ??
            fallbackCardNumber;

        if (!string.Equals(cardNumber, fallbackCardNumber, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var fullName =
            TryGetString(userElement, "fullName") ??
            TryGetString(userElement, "FullName") ??
            TryGetString(userElement, "name") ??
            TryGetString(userElement, "Name") ??
            "";
        var username =
            TryGetString(userElement, "username") ??
            TryGetString(userElement, "Username") ??
            "";
        var id =
            TryGetString(userElement, "id") ??
            TryGetString(userElement, "Id") ??
            TryGetString(userElement, "userId") ??
            TryGetString(userElement, "UserId") ??
            "";
        var email =
            TryGetString(userElement, "email") ??
            TryGetString(userElement, "Email") ??
            TryGetString(userElement, "emailAddress") ??
            TryGetString(userElement, "EmailAddress") ??
            "";

        return string.IsNullOrWhiteSpace(fullName) &&
               string.IsNullOrWhiteSpace(username) &&
               string.IsNullOrWhiteSpace(id) &&
               string.IsNullOrWhiteSpace(email)
            ? null
            : new ReaderIdentityInfo(cardNumber, fullName, username, id, email);
    }

    private static string? TryGetNestedString(JsonElement root, string objectName, string propertyName)
    {
        return root.TryGetProperty(objectName, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? TryGetString(nested, propertyName)
            : null;
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private async Task<int> GetMonthlyBorrowLimitAsync(CancellationToken cancellationToken)
    {
        var configuredDefault = _configuration.GetValue<int?>("Borrowing:MonthlyLimit") ?? DefaultMonthlyBorrowLimit;

        var policyEvent = await _dbContext.PublishedEventLogs
            .AsNoTracking()
            .Where(item => item.SourceService == "N2.Circulation" && item.EventType == BorrowPolicyEventType)
            .OrderByDescending(item => item.PublishedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (policyEvent is null)
        {
            return Math.Clamp(configuredDefault, 1, 100);
        }

        try
        {
            using var document = JsonDocument.Parse(policyEvent.PayloadJson);
            var root = document.RootElement;
            var limit = root.TryGetProperty("MonthlyBorrowLimit", out var pascalLimit)
                ? pascalLimit.GetInt32()
                : root.TryGetProperty("monthlyBorrowLimit", out var camelLimit)
                    ? camelLimit.GetInt32()
                    : configuredDefault;

            return Math.Clamp(limit, 1, 100);
        }
        catch (JsonException)
        {
            return Math.Clamp(configuredDefault, 1, 100);
        }
    }

    private async Task SetMonthlyBorrowLimitAsync(int monthlyBorrowLimit, CancellationToken cancellationToken)
    {
        _dbContext.PublishedEventLogs.Add(new PublishedEventLog
        {
            Id = Guid.NewGuid(),
            SourceService = "N2.Circulation",
            EventType = BorrowPolicyEventType,
            PayloadJson = JsonSerializer.Serialize(new { MonthlyBorrowLimit = monthlyBorrowLimit }),
            PublishedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static DateTime NormalizeUtc(DateTime date)
    {
        return date.Kind switch
        {
            DateTimeKind.Utc => date,
            DateTimeKind.Local => date.ToUniversalTime(),
            _ => DateTime.SpecifyKind(date, DateTimeKind.Utc)
        };
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

    private sealed record ReaderIdentityInfo(string CardNumber, string FullName, string Username, string Id, string Email);

    private static ReviewDto? TryReadReview(string payloadJson)
    {
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            return TryReadReview(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ReviewDto? TryReadReview(JsonElement root, string? fallbackBookId = null)
    {
        var bookId = ReadString(root, "bookId", "BookId") ?? fallbackBookId;
        if (string.IsNullOrWhiteSpace(bookId))
        {
            return null;
        }

        var createdAt = DateTimeOffset.MinValue;
        if (TryGetProperty(root, out var createdAtElement, "createdAt", "CreatedAt") &&
            createdAtElement.ValueKind == JsonValueKind.String &&
            createdAtElement.TryGetDateTimeOffset(out var parsedCreatedAt))
        {
            createdAt = parsedCreatedAt;
        }

        var reviewId = Guid.Empty;
        if (TryGetProperty(root, out var reviewIdElement, "reviewId", "ReviewId", "id", "Id") &&
            reviewIdElement.ValueKind == JsonValueKind.String &&
            reviewIdElement.TryGetGuid(out var parsedReviewId))
        {
            reviewId = parsedReviewId;
        }

        return new ReviewDto
        {
            Id = ReadString(root, "id", "Id", "reviewId", "ReviewId") ?? string.Empty,
            ReviewId = reviewId,
            BookId = bookId,
            UserId = ReadString(root, "userId", "UserId"),
            CardNumber = ReadString(root, "cardNumber", "CardNumber"),
            Username = ReadString(root, "username", "Username", "userName", "UserName"),
            FullName = ReadString(root, "fullName", "FullName"),
            Rating = Math.Clamp(ReadInt(root, "rating", "Rating"), 0, 5),
            Comment = ReadString(root, "comment", "Comment") ?? string.Empty,
            CreatedAt = createdAt
        };
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        if (!TryGetProperty(element, out var value, names))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int ReadInt(JsonElement element, params string[] names)
    {
        if (!TryGetProperty(element, out var value, names))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return int.TryParse(value.GetString(), out var parsed) ? parsed : 0;
    }

    private static double ReadDecimal(JsonElement element, params string[] names)
    {
        if (!TryGetProperty(element, out var value, names))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        return double.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static List<JsonElement> ReadArray(JsonElement element, params string[] names)
    {
        if (!TryGetProperty(element, out var value, names) || value.ValueKind != JsonValueKind.Array)
        {
            return new List<JsonElement>();
        }

        return value.EnumerateArray().ToList();
    }

    private sealed class ReviewDto
    {
        public string Id { get; init; } = string.Empty;
        public Guid ReviewId { get; init; }
        public string BookId { get; init; } = string.Empty;
        public string? UserId { get; init; }
        public string? CardNumber { get; init; }
        public string? Username { get; init; }
        public string? FullName { get; init; }
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

