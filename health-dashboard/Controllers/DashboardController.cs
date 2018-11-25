using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using health_dashboard.Models;
using Newtonsoft.Json;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using System.Net;
using Microsoft.Extensions.Primitives;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace health_dashboard.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private static readonly HttpClient client = new HttpClient();
        
        [Authorize("Administrator")]
        public IActionResult Admin()
        {
            return View();
        }
        
        public async Task<IActionResult> Goals()
        {
            GoalsViewModel vm = new GoalsViewModel();
            vm.ActivityTypes = await GetActivityTypes();
            vm.Challenges = await GetChallenges();

            if (Request.Method == "POST")
            {
                if (StringValues.IsNullOrEmpty(Request.Form["goal-metric"]))
                {
                    vm.Message = "Please choose a goal metric.";
                    return View(vm);
                }

                var response = await PostFormChallenge();

                // Add success / failure message
                if ((int)response.StatusCode == 201)
                {
                    vm.Message = "Success";
                }
                else
                {
                    vm.Message = "Goal save failed. Please contact a coordinator or an administrator.";
                }
            }

            return View(vm);
        }
        
        [HttpPost]
        public async Task<IActionResult> GoalDeleteAjax(int id)
        {
            var response = await DeleteGoal(id);

            if ((int)response.StatusCode != 201)
            {
                return BadRequest();
            }
            return Ok();
        }
        
        public async Task<IActionResult> Index()
        {
            IndexViewModel vm = new IndexViewModel();

            vm.ActivityTypes = await GetActivityTypes();

            List<HealthActivity> api_activities = await GetActivities();

            Dictionary<string, Dictionary<string, List<HealthActivity>>> activities_by_type = new Dictionary<string, Dictionary<string, List<HealthActivity>>>();
            /*
             *  activities_by_type = [
             *      [type] => [
             *          [date] => [
             *              HealthActivity,
             *          ],
             *      ],
             *  ]
             *  
             **/

            foreach (var a in api_activities)
            {
                if (!activities_by_type.ContainsKey(a.activity_type))
                {
                    activities_by_type.Add(a.activity_type, new Dictionary<string, List<HealthActivity>>());
                }

                DateTime startTime = DateTime.Parse(a.start_time);
                if (!activities_by_type[a.activity_type].ContainsKey(startTime.ToShortDateString()))
                {
                    activities_by_type[a.activity_type].Add(startTime.ToShortDateString(), new List<HealthActivity>());
                }

                activities_by_type[a.activity_type][startTime.ToShortDateString()].Add(a);
            }

            vm.Activities = activities_by_type;

            vm.Challenges = await GetChallenges();

            return View(vm);
        }
        
        public async Task<IActionResult> Input()
        {
            InputViewModel vm = new InputViewModel();

            vm.ActivityTypes = await GetActivityTypes();
            
            if (Request.Method == "POST")
            {
                var response = await PostFormActivity();
                
                // Add success / failure message
                if ((int) response.StatusCode == 201)
                {
                    vm.Message = "Success";
                } else
                {
                    vm.Message = "Activity save failed. Please contact a coordinator or an administrator.";
                }
            }
            
            return View(vm);
        }
        
        [HttpPost]
        public async Task<IActionResult> InputAjax()
        {
            var response = await PostFormActivity();
            
            if ((int)response.StatusCode != 201)
            {
                return BadRequest();
            }
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

        /* ------ API Calls and Responses ------ */
        private async Task<HttpResponseMessage> DeleteGoal(int id)
        {
            if (Environment.GetEnvironmentVariable("deployment") != null)
            {
                return await client.DeleteAsync("challenges/" + id);
            }

            HttpResponseMessage r = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Moved
            };
            return r;
        }

        private async Task<List<HealthActivity>> GetActivities()
        {
            string api_activities_json;
            if ( Environment.GetEnvironmentVariable("deployment") != null )
            {
                api_activities_json = await client.GetStringAsync("health-data-repositry/activity/find/{UUID}");
            }
            else
            {
                api_activities_json = System.IO.File.ReadAllText("./activity-find-1.json");
            }
            return (List<HealthActivity>)JsonConvert.DeserializeObject(api_activities_json, typeof(List<HealthActivity>));
        }

        private async Task<List<object>> GetActivityTypes()
        {
            string activity_types_json;
            if ( Environment.GetEnvironmentVariable("deployment") != null )
            {
                activity_types_json = await client.GetStringAsync("health-data-repositry/activity-types");
            }
            else
            {
                activity_types_json = System.IO.File.ReadAllText("./activity-types.json");
            }
            return (List<object>)JsonConvert.DeserializeObject(activity_types_json, typeof(List<object>));
        }
        
        private async Task<List<Challenge>> GetChallenges()
        {
            string challenges_json;
            if (Environment.GetEnvironmentVariable("deployment") != null)
            {
                challenges_json = await client.GetStringAsync("challenges/find/{UUID}");
            }
            else
            {
                challenges_json = System.IO.File.ReadAllText("./challenge-find-1.json");
            }
            return (List<Challenge>)JsonConvert.DeserializeObject(challenges_json, typeof(List<Challenge>));
        }

        private async Task<HttpResponseMessage> PostFormActivity()
        {
            var activity = new NewHealthActivity
            {
                start_time = Request.Form["start-time"],
                activity_type = Request.Form["activity-type"],
                distance = Request.Form["distance"],
                duration = Request.Form["duration"],
                quantity = int.Parse(Request.Form["quantity"])
            };
            var activity_json = JsonConvert.SerializeObject(activity);

            if (Environment.GetEnvironmentVariable("deployment") != null)
            {
                return await client.PostAsync("health-data-repositry/activity", new StringContent(activity_json, Encoding.UTF8, "application/json"));
            }

            HttpResponseMessage r = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Moved
            };
            return r;
        }

        /* Needs changing to JSON */
        private async Task<HttpResponseMessage> PostFormChallenge()
        {
            var values = new Dictionary<string, string>
            {
                { "startDateTime", Request.Form["start-time"] },
                { "endDateTime", Request.Form["end-time"] },
                { "goal", Request.Form["target"] },
                { "activity[activityName]", Request.Form["activity-type"] }
            };

            if (Request.Form["goal-metric"] == "distance")
            {
                values.Add("activity[goalMetric]", "Metres");
            } else if (Request.Form["goal-metric"] == "duration")
            {
                values.Add("activity[goalMetric]", "Minutes");
            } else if (Request.Form["goal-metric"] == "quantity")
            {
                values.Add("activity[goalMetric]", Request.Form["activity-type"]);
            }

            var content = new FormUrlEncodedContent(values);
            if (Environment.GetEnvironmentVariable("deployment") != null)
            {
                return await client.PostAsync("health-data-repositry/activity", content);
            }

            HttpResponseMessage r = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Moved
            };
            return r;
        }
    }

    // Is this a bodge?
    public class GoalsViewModel
    {
        public List<object> ActivityTypes { get; set; }
        public List<Challenge> Challenges { get; set; }
        public string Message { get; set; }
    }

    public class IndexViewModel
    {
        public Dictionary<string, Dictionary<string, List<HealthActivity>>> Activities { get; set; }
        public List<object> ActivityTypes { get; set; }
        public List<Challenge> Challenges { get; set; }
    }

    public class InputViewModel
    {
        public List<object> ActivityTypes { get; set; }
        public string Message { get; set; }
    }
}
