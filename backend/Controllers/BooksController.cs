using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace N2.Circulation.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/books")]
public sealed class BooksController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    private string CatalogServiceUrl =>
        _configuration["CatalogService:BaseUrl"]
        ?? _configuration["CatalogService__BaseUrl"]
        ?? "http://localhost:5185";

    public BooksController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            ForwardAuthorizationHeader(client);
            var response = await client.GetAsync($"{CatalogServiceUrl}/api/books", cancellationToken);

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, new { Message = "Catalog service unavailable." });

            var books = await response.Content.ReadFromJsonAsync<List<object>>(cancellationToken);
            return Ok(books);
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { Message = "Cannot connect to Catalog Service." });
        }
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            ForwardAuthorizationHeader(client);
            var response = await client.GetAsync($"{CatalogServiceUrl}/api/books/{id}", cancellationToken);

            if (!response.IsSuccessStatusCode)
                return NotFound(new { Message = "Book not found." });

            var book = await response.Content.ReadFromJsonAsync<object>(cancellationToken);
            return Ok(book);
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { Message = "Cannot connect to Catalog Service." });
        }
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult> SearchAsync([FromQuery] string? q, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            ForwardAuthorizationHeader(client);
            var url = $"{CatalogServiceUrl}/api/books/search?q={Uri.EscapeDataString(q ?? string.Empty)}";
            using var response = await client.GetAsync(url, cancellationToken);
            return await ForwardCatalogResponseAsync(response, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { Message = "Cannot connect to Catalog Service." });
        }
    }

    [HttpGet("products")]
    [AllowAnonymous]
    public async Task<ActionResult> GetProductsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            ForwardAuthorizationHeader(client);
            using var response = await client.GetAsync($"{CatalogServiceUrl}/api/books/products", cancellationToken);
            return await ForwardCatalogResponseAsync(response, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { Message = "Cannot connect to Catalog Service." });
        }
    }

    [HttpPost("{id:int}/borrow")]
    [Authorize(Roles = "Librarian,Admin,librarian,admin")]
    public async Task<ActionResult> BorrowAsync(int id, [FromBody] QuantityRequest? request, CancellationToken cancellationToken)
    {
        return await ForwardBookQuantityMutationAsync(id, "borrow", request?.Quantity ?? 1, cancellationToken);
    }

    [HttpPost("{id:int}/return")]
    [Authorize(Roles = "Librarian,Admin,librarian,admin")]
    public async Task<ActionResult> ReturnAsync(int id, [FromBody] QuantityRequest? request, CancellationToken cancellationToken)
    {
        return await ForwardBookQuantityMutationAsync(id, "return", request?.Quantity ?? 1, cancellationToken);
    }

    [HttpPost("{id:int}/rating")]
    public async Task<ActionResult> RatingAsync(int id, [FromBody] RatingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            ForwardAuthorizationHeader(client);
            using var content = CreateJsonContent(new { rating = request.Rating });
            using var response = await client.PostAsync($"{CatalogServiceUrl}/api/books/{id}/rating", content, cancellationToken);
            return await ForwardCatalogResponseAsync(response, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { Message = "Cannot connect to Catalog Service." });
        }
    }

    [HttpGet("{id:int}/reviews")]
    [AllowAnonymous]
    public async Task<ActionResult> GetReviewsAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            ForwardAuthorizationHeader(client);
            using var response = await client.GetAsync($"{CatalogServiceUrl}/api/books/{id}/reviews", cancellationToken);
            return await ForwardCatalogResponseAsync(response, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { Message = "Cannot connect to Catalog Service." });
        }
    }

    [HttpPost("{id:int}/reviews")]
    public async Task<ActionResult> PostReviewAsync(int id, [FromBody] object request, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            ForwardAuthorizationHeader(client);
            using var content = CreateJsonContent(request);
            using var response = await client.PostAsync($"{CatalogServiceUrl}/api/books/{id}/reviews", content, cancellationToken);
            return await ForwardCatalogResponseAsync(response, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { Message = "Cannot connect to Catalog Service." });
        }
    }

    [HttpPost("{id:int}/write-off")]
    [Authorize(Roles = "Librarian,Admin,librarian,admin")]
    public async Task<ActionResult> WriteOffAsync(int id, [FromBody] object request, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            ForwardAuthorizationHeader(client);
            using var content = CreateJsonContent(request);
            using var response = await client.PostAsync($"{CatalogServiceUrl}/api/books/{id}/write-off", content, cancellationToken);
            return await ForwardCatalogResponseAsync(response, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { Message = "Cannot connect to Catalog Service." });
        }
    }

    private async Task<ActionResult> ForwardBookQuantityMutationAsync(int id, string action, int quantity, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            ForwardAuthorizationHeader(client);
            using var content = CreateJsonContent(new { quantity = Math.Max(1, quantity) });
            using var response = await client.PostAsync($"{CatalogServiceUrl}/api/books/{id}/{action}", content, cancellationToken);
            return await ForwardCatalogResponseAsync(response, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { Message = "Cannot connect to Catalog Service." });
        }
    }

    private static StringContent CreateJsonContent<T>(T payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }

    private void ForwardAuthorizationHeader(HttpClient client)
    {
        if (Request.Headers.TryGetValue("Authorization", out var authorization) &&
            !string.IsNullOrWhiteSpace(authorization.ToString()))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization.ToString());
        }
    }

    private static async Task<ActionResult> ForwardCatalogResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return new StatusCodeResult((int)response.StatusCode);
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
        return new ContentResult
        {
            Content = body,
            ContentType = contentType,
            StatusCode = (int)response.StatusCode
        };
    }

    public sealed record QuantityRequest(int Quantity);

    public sealed record RatingRequest(int Rating);
}
