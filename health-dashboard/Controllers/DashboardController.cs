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
using health_dashboard.Services;

namespace health_dashboard.Controllers
{

    [Authorize]
    public class DashboardController : Controller
    {
        private static IApiClient Client;
        private static IConfiguration AppConfig;

        public DashboardController(IConfiguration config, IApiClient client )
        {
            AppConfig = config.GetSection("Health_Dashboard");
            Client = client;
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
            List<HealthActivity> activities;
            if (!String.IsNullOrEmpty(AppConfig.GetValue<string>("HealthDataRepositoryUrl")))
            {
                var response = await Client.GetAsync(AppConfig.GetValue<string>("HealthDataRepositoryUrl") + "activity/find/" + User.Claims.FirstOrDefault(c => c.Type == "sub").Value);
                activities = await response.Content.ReadAsAsync<List<HealthActivity>>();
            }
            else
            {
                var activities_json = System.IO.File.ReadAllText("./activity-find-1.json");
                activities = (List<HealthActivity>)JsonConvert.DeserializeObject(activities_json, typeof(List<HealthActivity>));
            }
            return activities;
        }

        private async Task<List<ActivityType>> GetActivityTypes()
        {
            List<ActivityType> activity_types;
            if (!String.IsNullOrEmpty(AppConfig.GetValue<string>("HealthDataRepositoryUrl")))
            {
                var response = await Client.GetAsync(AppConfig.GetValue<string>(AppConfig.GetValue<string>("HealthDataRepositoryUrl")) + "activity-types");
                activity_types = await response.Content.ReadAsAsync<List<ActivityType>>();
            }
            else
            {
                var activity_types_json = System.IO.File.ReadAllText("./activity-types.json");
                activity_types = (List<ActivityType>)JsonConvert.DeserializeObject(activity_types_json, typeof(List<ActivityType>));
            }
            return activity_types;
        }
        
        private async Task<List<Challenge>> GetChallenges()
        {
            List<Challenge> challenges;
            if (!String.IsNullOrEmpty(AppConfig.GetValue<string>("ChallengesUrl")))
            {
                var response = await Client.GetAsync(AppConfig.GetValue<string>("ChallengesUrl") + "find/" + User.Claims.FirstOrDefault(c => c.Type == "sub").Value);
                challenges = await response.Content.ReadAsAsync<List<Challenge>>();
            }
            else
            {
                var challenges_json = System.IO.File.ReadAllText("./challenge-find-1.json");
                challenges = (List<Challenge>)JsonConvert.DeserializeObject(challenges_json, typeof(List<Challenge>));
            }
            return challenges;
        }

        private async Task<HttpResponseMessage> PostFormActivity()
        {
            // Parsing null string problems
            HealthActivity activity = new HealthActivity
            {
                UserId = User.Claims.FirstOrDefault(c => c.Type == "sub").Value,
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

            if (!String.IsNullOrEmpty(AppConfig.GetValue<string>("HealthDataRepositoryUrl")))
            {
                return await Client.PostAsync<HealthActivity>(AppConfig.GetValue<string>("HealthDataRepositoryUrl") + "activity", activity);
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
            Challenge challenge = new Challenge
            {
                userId = User.Claims.FirstOrDefault(c => c.Type == "sub").Value,
                startDateTime = Request.Form["start-time"],
                endDateTime = Request.Form["end-time"],
                goal = int.Parse(Request.Form["target"]),
                activity = new ChallengeActivity {
                    activityId = int.Parse(Request.Form["activity-type"]),
                    activityName = Request.Form["goal-metric"]
                }
            };
            
            if (!String.IsNullOrEmpty(AppConfig.GetValue<string>("ChallengesUrl")))
            {
                return await Client.PostAsync<Challenge>(AppConfig.GetValue<string>("ChallengesUrl") + "challenge", challenge);
            }

            HttpResponseMessage r = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable
            };
            return r;
        }
    }

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
