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
            MyViewModel vm = new MyViewModel();

            string activity_json = System.IO.File.ReadAllText("./exampleActivityData.json");
            List<object> activity = (List<object>)JsonConvert.DeserializeObject(activity_json, typeof(List<object>));
            vm.Activities = activity;

            string challenge_json = System.IO.File.ReadAllText("./exampleChallengeData.json");
            List<object> challenge = (List<object>)JsonConvert.DeserializeObject(challenge_json, typeof(List<object>));
            vm.Challenges = challenge;

            return View(vm);
        }

        public IActionResult Input()
        {
            return View();
        }

        [HttpPost]
        public IActionResult InputAjax()
        {
            // Ping off to HDR

            return Ok();
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

    // TEMPORARY BODGE (in this class, at least)
    public class MyViewModel
    {
        public List<object> Activities { get; set; }
        public List<object> Challenges { get; set; }
    }
}
