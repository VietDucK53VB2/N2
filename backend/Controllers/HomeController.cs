using Microsoft.AspNetCore.Mvc;

namespace N2.Circulation.Api.Controllers;

[ApiController]
public sealed class HomeController : ControllerBase
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        return Redirect("/login");
    }

    [HttpGet("/ping")]
    public IActionResult Ping()
    {
        return Ok("pong");
    }

    [HttpGet("/login")]
    public IActionResult Login()
    {
        var query = Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty;
        return Redirect($"/ui/librarian/{query}#/");
    }

    [HttpGet("/ui/librarian/ping")]
    public IActionResult LibrarianPing()
    {
        return Ok("pong");
    }
}
