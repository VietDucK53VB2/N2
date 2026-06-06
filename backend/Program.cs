using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
    options.JsonSerializerOptions.DictionaryKeyPolicy = null;
});
builder.Services.AddDbContext<N2.Circulation.Api.Data.CirculationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("CirculationDb");

    if (!string.IsNullOrWhiteSpace(connectionString) && connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(connectionString);
    }
    else if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, "circulation.db");
        options.UseSqlite($"Data Source={dbPath}");
    }
});

// Swagger with XML comments
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Log incoming requests for debugging host vs container routing issues
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("RequestLogger");
app.Use(async (context, next) =>
{
    logger.LogInformation("Incoming request {Method} {Path} Host={Host}", context.Request.Method, context.Request.Path, context.Request.Host);
    Console.WriteLine($"[REQ] {context.Request.Method} {context.Request.Path} Host={context.Request.Host}");
    await next();
});

// Serve static UI files under wwwroot
app.UseStaticFiles();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger"; // serve at /swagger
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "N2.Circulation.Api v1");
    options.EnableDeepLinking();
    options.DisplayRequestDuration();
});

app.UseAuthorization();

app.MapControllers();

// Fallback: serve index.html for root and UI role pages
app.Use(async (context, next) =>
{
    if (context.Response.HasStarted) { await next(); return; }

    var path = context.Request.Path.Value ?? "/";

    // Root → wwwroot/index.html
    if (path == "/" || path == "/index.html")
    {
        var rootIndex = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html");
        if (File.Exists(rootIndex))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
            await context.Response.SendFileAsync(rootIndex);
            return;
        }
    }

    // /ui/{role}/ → wwwroot/ui/{role}/index.html
    var segments = path.Trim('/').Split('/');
    if (segments.Length == 2)
    {
        var indexPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", segments[0], segments[1], "index.html");
        if (File.Exists(indexPath))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
            await context.Response.SendFileAsync(indexPath);
            return;
        }
    }

    // Try direct file
    var cleanPath = path.TrimStart('/');
    var directPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", cleanPath);
    if (File.Exists(directPath))
    {
        var ext = Path.GetExtension(directPath).ToLower();
        var ct = ext switch
        {
            ".html" => "text/html; charset=utf-8",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".png" => "image/png",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream"
        };
        context.Response.ContentType = ct;
        await context.Response.SendFileAsync(directPath);
        return;
    }

    await next();
});

using (var scope = app.Services.CreateScope())
{
    if (app.Environment.IsDevelopment())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<N2.Circulation.Api.Data.CirculationDbContext>();

        // Keep database initialization only for local development.
        // Production must not block startup or alter an existing shared database.
        if (dbContext.Database.IsInMemory())
        {
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        await N2.Circulation.Api.Data.CirculationSeeder.SeedAsync(dbContext);
    }
}

app.Run();
