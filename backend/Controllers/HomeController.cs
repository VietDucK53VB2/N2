using Microsoft.AspNetCore.Mvc;

namespace N2.Circulation.Api.Controllers;

[ApiController]
public sealed class HomeController : ControllerBase
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        return Redirect("http://163.223.210.87/login");
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
        return Redirect($"http://163.223.210.87/login{query}");
    }

    [HttpGet("/ui/librarian/ping")]
    public IActionResult LibrarianPing()
    {
        return Ok("pong");
    }
}
