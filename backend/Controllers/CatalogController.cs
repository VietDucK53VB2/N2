using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace N2.Circulation.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/catalog/books")]
public sealed class CatalogController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    private string CatalogServiceUrl =>
        _configuration["CatalogService:BaseUrl"]
        ?? _configuration["CatalogService__BaseUrl"]
        ?? "http://localhost:5185";

    public CatalogController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
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
            if (Request.Headers.TryGetValue("Authorization", out var authorization) &&
                !string.IsNullOrWhiteSpace(authorization.ToString()))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization.ToString());
            }

            var response = await client.GetAsync($"{CatalogServiceUrl}/api/books", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, new { Message = "Catalog service unavailable." });

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
            return new ContentResult
            {
                Content = body,
                ContentType = contentType,
                StatusCode = (int)response.StatusCode
            };
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
            if (Request.Headers.TryGetValue("Authorization", out var authorization) &&
                !string.IsNullOrWhiteSpace(authorization.ToString()))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization.ToString());
            }

            var response = await client.GetAsync($"{CatalogServiceUrl}/api/books/products", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, new { Message = "Catalog service unavailable." });

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
            return new ContentResult
            {
                Content = body,
                ContentType = contentType,
                StatusCode = (int)response.StatusCode
            };
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { Message = "Cannot connect to Catalog Service." });
        }
    }
}
