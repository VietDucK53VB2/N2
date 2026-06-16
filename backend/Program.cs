using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();

var jwtKey = builder.Configuration["Jwt:Key"] ?? builder.Configuration["Jwt__Key"] ?? "Your_Super_Secret_Key_For_JWT_Validation_Needs_To_Be_Long_Enough";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? builder.Configuration["Jwt__Issuer"] ?? "IdentityReportService";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? builder.Configuration["Jwt__Audience"] ?? "LibraryMicroservices";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.NameIdentifier
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is ClaimsIdentity identity)
                {
                    var roleValues = context.Principal.Claims
                        .Where(claim =>
                            claim.Type == ClaimTypes.Role ||
                            claim.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" ||
                            claim.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role" ||
                            claim.Type == "role" ||
                            claim.Type == "roles")
                        .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var role in roleValues)
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, role));
                        identity.AddClaim(new Claim(ClaimTypes.Role, ToTitleRole(role)));
                    }
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

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

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Example: Bearer {token}",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
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

// Serve static UI files under wwwroot.
// During active integration, avoid browser cache keeping old Vue bundles alive after deploy.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var requestPath = context.Context.Request.Path.Value ?? "";
        if (requestPath.StartsWith("/ui/", StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            context.Context.Response.Headers.Pragma = "no-cache";
            context.Context.Response.Headers.Expires = "0";
        }
    }
});

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger"; // serve at /swagger
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "N2.Circulation.Api v1");
    options.EnableDeepLinking();
    options.DisplayRequestDuration();
});

app.UseAuthentication();
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    var shouldValidateActiveAccount =
        path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) &&
        context.User.Identity?.IsAuthenticated == true;

    if (!shouldValidateActiveAccount)
    {
        await next();
        return;
    }

    var identityBaseUrl =
        app.Configuration["IdentityService:BaseUrl"] ??
        app.Configuration["IdentityService__BaseUrl"] ??
        "http://identity-service:80";
    var validatePath =
        app.Configuration["IdentityService:ValidatePath"] ??
        app.Configuration["IdentityService__ValidatePath"] ??
        "/api/Auth/validate";
    var validateUrl = validatePath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
        ? validatePath
        : $"{identityBaseUrl.TrimEnd('/')}/{validatePath.TrimStart('/')}";

    try
    {
        var clientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
        var client = clientFactory.CreateClient();
        if (context.Request.Headers.TryGetValue("Authorization", out var authorization))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization.ToString());
        }

        using var response = await client.GetAsync(validateUrl, context.RequestAborted);
        if (!response.IsSuccessStatusCode)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { Message = "Token is not active or user account has been disabled." });
            return;
        }

        var body = await response.Content.ReadAsStringAsync(context.RequestAborted);
        if (!string.IsNullOrWhiteSpace(body))
        {
            using var document = JsonDocument.Parse(body);
            if (TryReadIsActive(document.RootElement, out var isActive) && !isActive)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { Message = "User account has been disabled." });
                return;
            }
        }
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { Message = "Cannot validate user account status with Identity Service." });
        return;
    }

    await next();
});
app.UseAuthorization();

app.MapGet("/favicon.ico", () => Results.NoContent());
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
    var dbContext = scope.ServiceProvider.GetRequiredService<N2.Circulation.Api.Data.CirculationDbContext>();
    await N2.Circulation.Api.Data.CirculationSeeder.EnsureSchemaAsync(dbContext);

    if (app.Environment.IsDevelopment())
    {
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

static string ToTitleRole(string role)
{
    return role.Trim().ToLowerInvariant() switch
    {
        "admin" => "Admin",
        "librarian" => "Librarian",
        "reader" => "Reader",
        _ => role
    };
}

static bool TryReadIsActive(JsonElement root, out bool isActive)
{
    if (root.ValueKind == JsonValueKind.Object)
    {
        if (TryGetBoolean(root, "isActive", out isActive) || TryGetBoolean(root, "IsActive", out isActive))
        {
            return true;
        }

        if (root.TryGetProperty("user", out var user) && TryReadIsActive(user, out isActive))
        {
            return true;
        }

        if (root.TryGetProperty("data", out var data) && TryReadIsActive(data, out isActive))
        {
            return true;
        }
    }

    isActive = true;
    return false;
}

static bool TryGetBoolean(JsonElement root, string propertyName, out bool value)
{
    if (root.TryGetProperty(propertyName, out var element))
    {
        if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
        {
            value = element.GetBoolean();
            return true;
        }

        if (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out value))
        {
            return true;
        }
    }

    value = true;
    return false;
}
