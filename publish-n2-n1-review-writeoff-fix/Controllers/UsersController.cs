using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using N2.Circulation.Api.Data;
using N2.Circulation.Api.Models;

namespace N2.Circulation.Api.Controllers;

[ApiController]
[Authorize(Roles = "Librarian,Admin,librarian,admin")]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly CirculationDbContext _dbContext;

    public UsersController(CirculationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult> GetAllAsync(
        [FromQuery] string? role,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(role) && role != "all")
        {
            query = query.Where(u => u.Role == role);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                u.Username.ToLower().Contains(term) ||
                (u.Email != null && u.Email.ToLower().Contains(term)) ||
                (u.CardNumber != null && u.CardNumber.ToLower().Contains(term)));
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.FullName,
                u.Role,
                u.Email,
                u.CardNumber,
                u.CreatedAt,
                u.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.FullName,
                u.Role,
                u.Email,
                u.CardNumber,
                u.CreatedAt,
                u.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return NotFound(new { Message = "User not found." });
        }

        return Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult> CreateAsync([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new { Message = "Username is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return BadRequest(new { Message = "Password must be at least 6 characters." });
        }

        if (string.IsNullOrWhiteSpace(request.Role))
        {
            return BadRequest(new { Message = "Role is required." });
        }

        if (request.Role != "admin" && request.Role != "librarian" && request.Role != "reader")
        {
            return BadRequest(new { Message = "Invalid role. Must be admin, librarian, or reader." });
        }

        if (await _dbContext.Users.AnyAsync(u => u.Username == request.Username, cancellationToken))
        {
            return Conflict(new { Message = "Username already exists." });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username.Trim(),
            PasswordHash = HashPassword(request.Password),
            FullName = request.FullName?.Trim() ?? string.Empty,
            Role = request.Role,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            CardNumber = string.IsNullOrWhiteSpace(request.CardNumber) ? null : request.CardNumber.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsActive = request.IsActive
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetByIdAsync), new { id = user.Id }, new
        {
            user.Id,
            user.Username,
            user.FullName,
            user.Role,
            user.Email,
            user.CardNumber,
            user.CreatedAt,
            user.IsActive
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateAsync(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FindAsync([id], cancellationToken);

        if (user is null)
        {
            return NotFound(new { Message = "User not found." });
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.FullName = request.FullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (request.Role != "admin" && request.Role != "librarian" && request.Role != "reader")
            {
                return BadRequest(new { Message = "Invalid role." });
            }
            user.Role = request.Role;
        }

        if (request.Email is not null)
        {
            user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        }

        if (request.CardNumber is not null)
        {
            user.CardNumber = string.IsNullOrWhiteSpace(request.CardNumber) ? null : request.CardNumber.Trim();
        }

        if (request.IsActive is not null)
        {
            user.IsActive = request.IsActive.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (request.NewPassword.Length < 6)
            {
                return BadRequest(new { Message = "New password must be at least 6 characters." });
            }
            user.PasswordHash = HashPassword(request.NewPassword);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            user.Id,
            user.Username,
            user.FullName,
            user.Role,
            user.Email,
            user.CardNumber,
            user.CreatedAt,
            user.IsActive
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FindAsync([id], cancellationToken);

        if (user is null)
        {
            return NotFound(new { Message = "User not found." });
        }

        if (user.Role == "admin")
        {
            return BadRequest(new { Message = "Cannot delete an admin user." });
        }

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations: 100000,
            HashAlgorithmName.SHA256,
            outputLength: 32);

        var hashBytes = new byte[48];
        Buffer.BlockCopy(salt, 0, hashBytes, 0, 16);
        Buffer.BlockCopy(hash, 0, hashBytes, 16, 32);
        return Convert.ToBase64String(hashBytes);
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            var hashBytes = Convert.FromBase64String(storedHash);
            if (hashBytes.Length != 48) return false;
            var salt = new byte[16];
            Buffer.BlockCopy(hashBytes, 0, salt, 0, 16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations: 100000,
                HashAlgorithmName.SHA256,
                outputLength: 32);
            for (var i = 0; i < 32; i++)
            {
                if (hashBytes[i + 16] != hash[i]) return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public sealed record CreateUserRequest(
    string Username,
    string Password,
    string? FullName,
    string Role,
    string? Email,
    string? CardNumber,
    bool IsActive = true);

public sealed record UpdateUserRequest(
    string? FullName,
    string? Role,
    string? Email,
    string? CardNumber,
    bool? IsActive,
    string? NewPassword);
