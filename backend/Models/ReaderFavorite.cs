namespace N2.Circulation.Api.Models;

public sealed class ReaderFavorite
{
    public Guid Id { get; set; }

    public string UserKey { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public string? CardNumber { get; set; }

    public string? Username { get; set; }

    public string BookId { get; set; } = string.Empty;

    public string TenSach { get; set; } = string.Empty;

    public string? TacGia { get; set; }

    public string? ImageUrl { get; set; }

    public string? TheLoai { get; set; }

    public int SoBanConLai { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
