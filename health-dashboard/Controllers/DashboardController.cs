using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using health_dashboard.Models;

namespace health_dashboard.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Admin()
        {
            return View();
        }

        public IActionResult Goals()
        {
            return View();
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Input()
        {
            return View();
        }
        public IActionResult Rankings()
        {
            return View();
        }
        public IActionResult RemoveData()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
