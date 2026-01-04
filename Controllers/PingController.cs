// Controllers/PingController.cs
using Microsoft.AspNetCore.Mvc;

namespace ChessServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PingController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("Pong! Server is running.");
        }
    }
}