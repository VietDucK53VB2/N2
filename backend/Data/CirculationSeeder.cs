using Microsoft.EntityFrameworkCore;

namespace N2.Circulation.Api.Data;

public static class CirculationSeeder
{
    public static async Task SeedAsync(CirculationDbContext dbContext)
    {
        try { await dbContext.Database.ExecuteSqlRawAsync(@"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
            CREATE TABLE [Users] ([Id] uniqueidentifier NOT NULL PRIMARY KEY, [Username] nvarchar(64) NOT NULL,
            [PasswordHash] nvarchar(256) NOT NULL, [FullName] nvarchar(128), [Role] nvarchar(32) NOT NULL,
            [Email] nvarchar(128), [CardNumber] nvarchar(32), [CreatedAt] datetime2 NOT NULL, [IsActive] bit NOT NULL)"); }
        catch { /* ignore */ }

        await dbContext.SaveChangesAsync();
    }
}
