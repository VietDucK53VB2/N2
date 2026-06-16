using Microsoft.EntityFrameworkCore;

namespace N2.Circulation.Api.Data;

public static class CirculationSeeder
{
    public static async Task EnsureSchemaAsync(CirculationDbContext dbContext)
    {
        if (dbContext.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true)
        {
            try { await dbContext.Database.ExecuteSqlRawAsync(@"IF OBJECT_ID('dbo.ReaderFavorites', 'U') IS NULL
                CREATE TABLE [dbo].[ReaderFavorites] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [UserKey] nvarchar(128) NOT NULL,
                    [UserId] nvarchar(64) NULL,
                    [CardNumber] nvarchar(32) NULL,
                    [Username] nvarchar(64) NULL,
                    [BookId] nvarchar(64) NOT NULL,
                    [TenSach] nvarchar(256) NOT NULL,
                    [TacGia] nvarchar(128) NULL,
                    [ImageUrl] nvarchar(512) NULL,
                    [TheLoai] nvarchar(128) NULL,
                    [SoBanConLai] int NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL
                )"); }
            catch { /* ignore */ }

            try { await dbContext.Database.ExecuteSqlRawAsync(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ReaderFavorites_UserKey_BookId' AND object_id = OBJECT_ID('dbo.ReaderFavorites'))
                CREATE UNIQUE INDEX [IX_ReaderFavorites_UserKey_BookId] ON [dbo].[ReaderFavorites]([UserKey], [BookId])"); }
            catch { /* ignore */ }

            try { await dbContext.Database.ExecuteSqlRawAsync(@"IF COL_LENGTH('dbo.BorrowTransactions', 'ReaderName') IS NULL
                ALTER TABLE [dbo].[BorrowTransactions] ADD [ReaderName] nvarchar(128) NULL"); }
            catch { /* ignore */ }

            try { await dbContext.Database.ExecuteSqlRawAsync(@"IF COL_LENGTH('dbo.BorrowTransactions', 'ReaderUsername') IS NULL
                ALTER TABLE [dbo].[BorrowTransactions] ADD [ReaderUsername] nvarchar(64) NULL"); }
            catch { /* ignore */ }
        }
        else if (dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            try { await dbContext.Database.ExecuteSqlRawAsync(@"CREATE TABLE IF NOT EXISTS ReaderFavorites (
                Id TEXT NOT NULL PRIMARY KEY,
                UserKey TEXT NOT NULL,
                UserId TEXT NULL,
                CardNumber TEXT NULL,
                Username TEXT NULL,
                BookId TEXT NOT NULL,
                TenSach TEXT NOT NULL,
                TacGia TEXT NULL,
                ImageUrl TEXT NULL,
                TheLoai TEXT NULL,
                SoBanConLai INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )"); }
            catch { /* ignore */ }

            try { await dbContext.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_ReaderFavorites_UserKey_BookId ON ReaderFavorites(UserKey, BookId)"); }
            catch { /* ignore */ }

            try { await dbContext.Database.ExecuteSqlRawAsync(@"ALTER TABLE BorrowTransactions ADD COLUMN ReaderName TEXT NULL"); }
            catch { /* ignore when the column already exists */ }

            try { await dbContext.Database.ExecuteSqlRawAsync(@"ALTER TABLE BorrowTransactions ADD COLUMN ReaderUsername TEXT NULL"); }
            catch { /* ignore when the column already exists */ }
        }
    }

    public static async Task SeedAsync(CirculationDbContext dbContext)
    {
        try { await dbContext.Database.ExecuteSqlRawAsync(@"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
            CREATE TABLE [Users] ([Id] uniqueidentifier NOT NULL PRIMARY KEY, [Username] nvarchar(64) NOT NULL,
            [PasswordHash] nvarchar(256) NOT NULL, [FullName] nvarchar(128), [Role] nvarchar(32) NOT NULL,
            [Email] nvarchar(128), [CardNumber] nvarchar(32), [CreatedAt] datetime2 NOT NULL, [IsActive] bit NOT NULL)"); }
        catch { /* ignore */ }

        await EnsureSchemaAsync(dbContext);

        await dbContext.SaveChangesAsync();
    }
}
