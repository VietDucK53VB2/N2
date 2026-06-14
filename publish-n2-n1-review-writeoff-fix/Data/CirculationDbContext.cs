using Microsoft.EntityFrameworkCore;
using N2.Circulation.Api.Models;

namespace N2.Circulation.Api.Data;

public sealed class CirculationDbContext : DbContext
{
    public CirculationDbContext(DbContextOptions<CirculationDbContext> options)
        : base(options)
    {
    }

    public DbSet<BorrowTransaction> BorrowTransactions => Set<BorrowTransaction>();

    public DbSet<FineCharge> FineCharges => Set<FineCharge>();

    public DbSet<PublishedEventLog> PublishedEventLogs => Set<PublishedEventLog>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Book> Books => Set<Book>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BorrowTransaction>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.BookId).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Isbn).HasMaxLength(32);
            entity.Property(item => item.UserId).HasMaxLength(64);
            entity.Property(item => item.CardNumber).HasMaxLength(32);
            entity.Property(item => item.ReaderName).HasMaxLength(128);
            entity.Property(item => item.ReaderUsername).HasMaxLength(64);
            entity.Property(item => item.FineAmount).HasPrecision(18, 2);
            entity.HasIndex(item => new { item.UserId, item.CardNumber });
            entity.HasIndex(item => item.BookId);
            entity.HasIndex(item => item.BorrowedAt);
        });

        modelBuilder.Entity<FineCharge>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.UserId).HasMaxLength(64);
            entity.Property(item => item.CardNumber).HasMaxLength(32);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Reason).HasMaxLength(256).IsRequired();
            entity.HasIndex(item => new { item.UserId, item.PaidAt });
        });

        modelBuilder.Entity<PublishedEventLog>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(64).IsRequired();
            entity.Property(item => item.PayloadJson).IsRequired();
            entity.Property(item => item.SourceService).HasMaxLength(64).IsRequired();
            entity.HasIndex(item => new { item.SourceService, item.EventType, item.PublishedAt });
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Username).HasMaxLength(64).IsRequired();
            entity.Property(item => item.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(item => item.FullName).HasMaxLength(128);
            entity.Property(item => item.Role).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Email).HasMaxLength(128);
            entity.Property(item => item.CardNumber).HasMaxLength(32);
            entity.HasIndex(item => item.Username).IsUnique();
            entity.HasIndex(item => item.Role);
        });

        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.TenSach).HasMaxLength(256).IsRequired();
            entity.Property(item => item.TacGia).HasMaxLength(128);
            entity.Property(item => item.NhaSanXuat).HasMaxLength(128);
            entity.Property(item => item.SoLuong);
        });

        base.OnModelCreating(modelBuilder);
    }
}
