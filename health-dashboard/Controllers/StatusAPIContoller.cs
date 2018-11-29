using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace booking_facilities.Controllers
{
    [Route("/[controller]")]
    [ApiController]
    public class StatusAPIController : ControllerBase
    {
        // GET: /Status
        [HttpGet]
        public IActionResult Get()
        {
            return Ok();
        }
    }
}