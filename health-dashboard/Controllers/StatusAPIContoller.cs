using Microsoft.AspNetCore.Mvc;

namespace booking_facilities.Controllers
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