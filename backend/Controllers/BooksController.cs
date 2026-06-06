using Microsoft.AspNetCore.Mvc;

namespace N2.Circulation.Api.Controllers;

[ApiController]
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
    public async Task<ActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
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
    public async Task<ActionResult> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
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
}
