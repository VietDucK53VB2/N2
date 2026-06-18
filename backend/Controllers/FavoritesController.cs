using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using N2.Circulation.Api.Data;
using N2.Circulation.Api.Models;

namespace N2.Circulation.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/circulation/favorites")]
public sealed class FavoritesController : ControllerBase
{
    private readonly CirculationDbContext _dbContext;

    public FavoritesController(CirculationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetAsync(CancellationToken cancellationToken)
    {
        var identity = ResolveIdentity();
        if (identity is null)
        {
            return Unauthorized(new { Message = "Unable to resolve current user." });
        }

        var items = await _dbContext.ReaderFavorites
            .AsNoTracking()
            .Where(item => item.UserKey == identity.UserKey)
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => new
            {
                item.Id,
                item.UserKey,
                item.UserId,
                item.CardNumber,
                item.Username,
                BookId = item.BookId,
                id = item.BookId,
                tenSach = item.TenSach,
                tacGia = item.TacGia,
                imageUrl = item.ImageUrl,
                theLoai = item.TheLoai,
                soBanConLai = item.SoBanConLai,
                item.CreatedAt,
                item.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult> UpsertAsync([FromBody] ReaderFavoriteRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BookId))
        {
            return BadRequest(new { Message = "BookId is required." });
        }

        var identity = ResolveIdentity();
        if (identity is null)
        {
            return Unauthorized(new { Message = "Unable to resolve current user." });
        }

        var normalizedBookId = request.BookId.Trim();
        var normalizedTitle = string.IsNullOrWhiteSpace(request.TenSach) ? normalizedBookId : request.TenSach.Trim();

        var item = await _dbContext.ReaderFavorites
            .FirstOrDefaultAsync(f => f.UserKey == identity.UserKey && f.BookId == normalizedBookId, cancellationToken);

        if (!request.IsFavorite)
        {
            if (item is not null)
            {
                _dbContext.ReaderFavorites.Remove(item);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return NoContent();
        }

        if (item is null)
        {
            item = new ReaderFavorite
            {
                Id = Guid.NewGuid(),
                UserKey = identity.UserKey,
                UserId = identity.UserId,
                CardNumber = identity.CardNumber,
                Username = identity.Username,
                BookId = normalizedBookId,
                TenSach = normalizedTitle,
                TacGia = string.IsNullOrWhiteSpace(request.TacGia) ? null : request.TacGia.Trim(),
                ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
                TheLoai = string.IsNullOrWhiteSpace(request.TheLoai) ? null : request.TheLoai.Trim(),
                SoBanConLai = Math.Max(0, request.SoBanConLai),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.ReaderFavorites.Add(item);
        }
        else
        {
            item.UserId = identity.UserId;
            item.CardNumber = identity.CardNumber;
            item.Username = identity.Username;
            item.TenSach = normalizedTitle;
            item.TacGia = string.IsNullOrWhiteSpace(request.TacGia) ? item.TacGia : request.TacGia.Trim();
            item.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? item.ImageUrl : request.ImageUrl.Trim();
            item.TheLoai = string.IsNullOrWhiteSpace(request.TheLoai) ? item.TheLoai : request.TheLoai.Trim();
            item.SoBanConLai = Math.Max(0, request.SoBanConLai);
            item.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            item.Id,
            item.UserKey,
            item.UserId,
            item.CardNumber,
            item.Username,
            BookId = item.BookId,
            id = item.BookId,
            tenSach = item.TenSach,
            tacGia = item.TacGia,
            imageUrl = item.ImageUrl,
            theLoai = item.TheLoai,
            soBanConLai = item.SoBanConLai,
            item.CreatedAt,
            item.UpdatedAt
        });
    }

    [HttpDelete("{bookId}")]
    public async Task<ActionResult> DeleteAsync(string bookId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bookId))
        {
            return BadRequest(new { Message = "BookId is required." });
        }

        var identity = ResolveIdentity();
        if (identity is null)
        {
            return Unauthorized(new { Message = "Unable to resolve current user." });
        }

        var item = await _dbContext.ReaderFavorites
            .FirstOrDefaultAsync(f => f.UserKey == identity.UserKey && f.BookId == bookId, cancellationToken);

        if (item is null)
        {
            return NoContent();
        }

        _dbContext.ReaderFavorites.Remove(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private IdentityInfo? ResolveIdentity()
    {
        var userId = FirstClaim(ClaimTypes.NameIdentifier, "sub", "nameid", "userId", "UserId");
        var cardNumber = FirstClaim("cardNumber", "CardNumber", "readerCard", "ReaderCard", "libraryCardNumber", "LibraryCardNumber");
        var username = FirstClaim(ClaimTypes.Name, "unique_name", "uniqueName", "preferred_username", "preferredUsername", "username", "Username");

        var userKey = FirstNonEmpty(userId, cardNumber, username);
        if (string.IsNullOrWhiteSpace(userKey))
        {
            return null;
        }

        return new IdentityInfo(
            UserKey: userKey,
            UserId: userId,
            CardNumber: cardNumber,
            Username: username
        );
    }

    private string FirstClaim(params string[] claimTypes)
    {
        foreach (var type in claimTypes)
        {
            var value = User.FindFirstValue(type);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private sealed record IdentityInfo(string UserKey, string UserId, string CardNumber, string Username);

    public sealed class ReaderFavoriteRequest
    {
        public string BookId { get; set; } = string.Empty;
        public string TenSach { get; set; } = string.Empty;
        public string? TacGia { get; set; }
        public string? ImageUrl { get; set; }
        public string? TheLoai { get; set; }
        public int SoBanConLai { get; set; }
        public bool IsFavorite { get; set; } = true;
    }
}
