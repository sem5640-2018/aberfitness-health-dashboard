using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using health_dashboard.Models;
using Newtonsoft.Json;

namespace health_dashboard.Controllers
{
    public class DashboardController : Controller
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
            string json = System.IO.File.ReadAllText("./exampleData.json");
            List<object> activity = (List<object>)JsonConvert.DeserializeObject(json, typeof(List<object>));

            return View(activity);
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
