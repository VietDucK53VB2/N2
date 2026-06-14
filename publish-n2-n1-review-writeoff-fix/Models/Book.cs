namespace N2.Circulation.Api.Models;

public sealed class Book
{
    public int Id { get; set; }
    public string TenSach { get; set; } = string.Empty;
    public string TacGia { get; set; } = string.Empty;
    public string NhaSanXuat { get; set; } = string.Empty;
    public int SoLuong { get; set; }
}
