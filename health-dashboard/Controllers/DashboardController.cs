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
using Microsoft.AspNetCore.Identity;
using X.PagedList;

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

            List<HealthActivity> api_activities = await GetUserActivities();

            Dictionary<string, List<HealthActivity>> activities_by_type = new Dictionary<string, List<HealthActivity>>();
            /*
             *  activities_by_type = [
             *      [date] => [
             *          HealthActivity,
             *      ],
             *  ]
             *  
             **/

            foreach (var a in api_activities)
            {
                DateTime startTime = DateTime.Parse(a.StartTimestamp);
                // Just date - not the time
                if (!activities_by_type.ContainsKey(startTime.ToShortDateString()))
                {
                    activities_by_type.Add(startTime.ToShortDateString(), new List<HealthActivity>());
                }

                activities_by_type[startTime.ToShortDateString()].Add(a);
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

        public async Task<IActionResult> RemoveData(int page = 1)
        {
            RemoveDataViewModel vm = new RemoveDataViewModel();

            var allActivities = await GetUserActivities();
            vm.Activities = allActivities.ToPagedList(page, 20);

            vm.ActivityTypes = await GetActivityTypes();

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> RemoveDataAjax(int id)
        {
            var response = await DeleteActivity(id);

            if ((int)response.StatusCode != 201)
            {
                return BadRequest();
            }
            return Ok();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        /* ------ API Calls and Responses ------ */
        private async Task<HttpResponseMessage> DeleteActivity(int id)
        {
            if (Environment.GetEnvironmentVariable("deployment") != null)
            {
                return await client.DeleteAsync("activity/" + id);
            }

            HttpResponseMessage r = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Moved
            };
            return r;
        }

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

        private async Task<List<HealthActivity>> GetUserActivities()
        {
            string api_activities_json;
            if (Environment.GetEnvironmentVariable("deployment") != null)
            {
                api_activities_json = await client.GetStringAsync("health-data-repositry/activity/find/{UUID}");
            }
            else
            {
                api_activities_json = System.IO.File.ReadAllText("./activity-find-1.json");
            }
            return (List<HealthActivity>)JsonConvert.DeserializeObject(api_activities_json, typeof(List<HealthActivity>));
        }

        private async Task<List<ActivityType>> GetActivityTypes()
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
            return (List<ActivityType>)JsonConvert.DeserializeObject(activity_types_json, typeof(List<ActivityType>));
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
            // Parsing null string problems
            var activity = new HealthActivity
            {
                UserId = 1, // Need to replace this with UserId
                StartTimestamp = Request.Form["start-time"],
                EndTimestamp = Request.Form["end-time"],
                Source = -1,
                ActivityType = StringValues.IsNullOrEmpty(Request.Form["activity-type"]) ? 0 : int.Parse(Request.Form["activity-type"]),
                CaloriesBurnt = StringValues.IsNullOrEmpty(Request.Form["calories-burnt"]) ? 0 : int.Parse(Request.Form["calories-burnt"]),
                AverageHeartRate = StringValues.IsNullOrEmpty(Request.Form["average-heart-rate"]) ? 0 : int.Parse(Request.Form["average-heart-rate"]),
                StepsTaken = StringValues.IsNullOrEmpty(Request.Form["steps-taken"]) ? 0 : int.Parse(Request.Form["steps-taken"]),
                MetresTravelled = StringValues.IsNullOrEmpty(Request.Form["metres-travelled"]) ? 0 : int.Parse(Request.Form["metres-travelled"]),
                MetresElevationGained = StringValues.IsNullOrEmpty(Request.Form["metres-elevation-gained"]) ? 0 : int.Parse(Request.Form["metres-elevation-gained"])
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
                { "activity[activityId]", Request.Form["activity-type"] },
                { "activity[goalMetric]", Request.Form["goal-metric"] }
            };

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
        public List<ActivityType> ActivityTypes { get; set; }
        public List<Challenge> Challenges { get; set; }
        public string Message { get; set; }
    }

    public class IndexViewModel
    {
        public Dictionary<string, List<HealthActivity>> Activities { get; set; }
        public List<ActivityType> ActivityTypes { get; set; }
        public List<Challenge> Challenges { get; set; }
    }

    public class InputViewModel
    {
        public List<ActivityType> ActivityTypes { get; set; }
        public string Message { get; set; }
    }

    public class RemoveDataViewModel
    {
        public IPagedList<HealthActivity> Activities { get; set; }
        public List<ActivityType> ActivityTypes { get; set; }
    }
}
