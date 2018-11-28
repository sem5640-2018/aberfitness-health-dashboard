using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using X.PagedList;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace health_dashboard.Controllers
{

    [Authorize]
    public class DashboardController : Controller
    {
        private static readonly HttpClient Client = new HttpClient();
        private static IConfiguration AppConfig;

        public DashboardController(IConfiguration config )
        {
            AppConfig = config.GetSection("Health_Dashboard");
        }
        
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
            if (!String.IsNullOrEmpty(AppConfig.GetValue<string>("HealthDataRepositoryUrl")))
            {
                return await Client.DeleteAsync(AppConfig.GetValue<string>("HealthDataRepositoryUrl") + "activity/" + id);
            }

            HttpResponseMessage r = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable
            };
            return r;
        }

        private async Task<HttpResponseMessage> DeleteGoal(int id)
        {
            if (!String.IsNullOrEmpty(AppConfig.GetValue<string>("ChallengesUrl")))
            {
                return await Client.DeleteAsync(AppConfig.GetValue<string>("ChallengesUrl") + "challenges/" + id);
            }

            HttpResponseMessage r = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable
            };
            return r;
        }

        private async Task<List<HealthActivity>> GetUserActivities()
        {
            string api_activities_json;
            if (!String.IsNullOrEmpty(AppConfig.GetValue<string>("HealthDataRepositoryUrl")))
            {
                api_activities_json = await Client.GetStringAsync(AppConfig.GetValue<string>("HealthDataRepositoryUrl") + "activity/find/" + User.Claims.FirstOrDefault(c => c.Type == "sid").Value);
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
            if (!String.IsNullOrEmpty(AppConfig.GetValue<string>("HealthDataRepositoryUrl")))
            {
                activity_types_json = await Client.GetStringAsync(!String.IsNullOrEmpty(AppConfig.GetValue<string>("HealthDataRepositoryUrl")) + "activity-types");
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
            if (!String.IsNullOrEmpty(AppConfig.GetValue<string>("ChallengesUrl")))
            {
                challenges_json = await Client.GetStringAsync(AppConfig.GetValue<string>("ChallengesUrl") + "find/" + User.Claims.FirstOrDefault(c => c.Type == "sid").Value);
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
                UserId = User.Claims.FirstOrDefault(c => c.Type == "sid").Value,
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

            if (!String.IsNullOrEmpty(AppConfig.GetValue<string>("HealthDataRepositoryUrl")))
            {
                return await Client.PostAsync(AppConfig.GetValue<string>("HealthDataRepositoryUrl") + "activity", new StringContent(activity_json, Encoding.UTF8, "application/json"));
            }

            HttpResponseMessage r = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable
            };
            return r;
        }

        /* Needs changing to JSON */
        private async Task<HttpResponseMessage> PostFormChallenge()
        {
            var challenge = new Challenge
            {
                userId = User.Claims.FirstOrDefault(c => c.Type == "sid").Value,
                startDateTime = Request.Form["start-time"],
                endDateTime = Request.Form["end-time"],
                goal = int.Parse(Request.Form["target"]),
                activity = new ChallengeActivity {
                    activityId = int.Parse(Request.Form["activity-type"]),
                    activityName = Request.Form["goal-metric"]
                }
            };

            var challenge_json = JsonConvert.SerializeObject(challenge);
            if (!String.IsNullOrEmpty(AppConfig.GetValue<string>("ChallengesUrl")))
            {
                return await Client.PostAsync(AppConfig.GetValue<string>("ChallengesUrl") + "challenge", new StringContent(challenge_json, Encoding.UTF8, "application/json"));
            }

            HttpResponseMessage r = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable
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
