using Microsoft.AspNetCore.Mvc;

namespace health_dashboard.Controllers
{
    [Route("/api/[controller]")]
    [ApiController]
    public class StatusAPIController : ControllerBase
    {
        // GET: /Status
        [HttpGet]
        public IActionResult Get()
        {
            return NoContent();
        }
    }
}