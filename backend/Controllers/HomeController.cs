using Microsoft.AspNetCore.Mvc;

namespace N2.Circulation.Api.Controllers;

[ApiController]
public sealed class HomeController : ControllerBase
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        return Redirect("/swagger/index.html");
    }

    [HttpGet("/ping")]
    public IActionResult Ping()
    {
        return Ok("pong");
    }

    [HttpGet("/ui/librarian/ping")]
    public IActionResult LibrarianPing()
    {
        return Ok("pong");
    }
}
