using Microsoft.EntityFrameworkCore;

namespace N2.Circulation.Api.Data;

public static class CirculationSeeder
{
    public static async Task EnsureSchemaAsync(CirculationDbContext dbContext)
    {
        if (dbContext.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true)
        {
            try { await dbContext.Database.ExecuteSqlRawAsync(@"IF COL_LENGTH('dbo.BorrowTransactions', 'ReaderName') IS NULL
                ALTER TABLE [dbo].[BorrowTransactions] ADD [ReaderName] nvarchar(128) NULL"); }
            catch { /* ignore */ }

            try { await dbContext.Database.ExecuteSqlRawAsync(@"IF COL_LENGTH('dbo.BorrowTransactions', 'ReaderUsername') IS NULL
                ALTER TABLE [dbo].[BorrowTransactions] ADD [ReaderUsername] nvarchar(64) NULL"); }
            catch { /* ignore */ }
        }
        else if (dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
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
