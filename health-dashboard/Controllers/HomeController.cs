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
using Newtonsoft.Json.Linq;

namespace health_dashboard.Controllers
{

    [Authorize]
    public class HomeController : Controller
    {
        private static IApiClient Client;
        private static IConfiguration Config;
        private static IConfigurationSection AppConfig;

        public HomeController(IConfiguration config, IApiClient client)
        {
            Config = config;
            AppConfig = Config.GetSection("Health_Dashboard");
            Client = client;
        }

        [Authorize("Administrator")]
        public IActionResult Admin()
        {
            // TODO Implement "Administrative Interface for managing users"
            /*
             * Within the context of this microservice, this should only require
             * deleting data à la the RemoveData() action.
             * Views should be reusable, just needs a parameter adding for the
             * user that the admin would like to remove the data for.
             *
             * If we need the admin to be able to edit the data, then it's going
             * to requre a little more work.
             */
            return View();
        }

        // Currently doesn't care about the timeframe, in terms of a pass/fail or showing how long is left.
        public async Task<IActionResult> Goals()
        {
            GoalsViewModel vm = new GoalsViewModel();

            if (Request.Method == "POST")
            {
                var response = await PostFormGoal();

                // Add success / failure message
                if ( response.IsSuccessStatusCode )
                {
                    vm.Message = "Success";
                }
                else
                {
                    vm.Message = "Goal save failed. Please contact a coordinator or an administrator.";
                }
            }

            vm.ActivityTypes = await GetValidChallengeActivityTypes();
            vm.Goals = await GetPersonalGoals();
            vm.GoalMetrics = await GetGoalMetrics();

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> GoalDeleteAjax(int id)
        {
            var response = await DeleteGoal(id);

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest();
            }
            return Ok();
        }

        // Currently only shows a graph for "Steps" activities
        public async Task<IActionResult> Index()
        {

            IndexViewModel vm = new IndexViewModel();

            vm.ActivityTypes = await GetHealthDataActivityTypes();
            vm.Challenges = await GetGroupChallenges();
            vm.ChallengeJoinUrl = AppConfig.GetValue<string>("ChallengeUrl") + "challengesManage";
            vm.Goals = await GetPersonalGoals();

            // This may want to be a different method, if only the last month of data is desired
            DateTime today = DateTime.Today;
            DateTime weekAgo = DateTime.Today.AddDays(-7);
            List<HealthActivity> api_activities = await GetUserActivities(User.Claims.FirstOrDefault(c => c.Type == "sub").Value, weekAgo.ToString("yyyy-MM-dd"), today.ToString("yyyy-MM-dd"));

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
                DateTime startTime = DateTime.Parse(a.startTimestamp);
                // Just date - not the time
                if (!activities_by_type.ContainsKey(startTime.ToShortDateString()))
                {
                    activities_by_type.Add(startTime.ToShortDateString(), new List<HealthActivity>());
                }

                activities_by_type[startTime.ToShortDateString()].Add(a);
            }

            vm.Activities = activities_by_type;

            // There isn't current a different result if no challenge/activity data is found.
            return View(vm);
        }

        public async Task<IActionResult> Input()
        {
            InputViewModel vm = new InputViewModel();

            vm.ActivityTypes = await GetHealthDataActivityTypes();

            if (Request.Method == "POST")
            {
                var response = await PostFormActivity();

                // Add success / failure message
                if (response.IsSuccessStatusCode)
                {
                    vm.Message = "Success";
                }
                else
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

            if (response.IsSuccessStatusCode)
            {
                return BadRequest();
            }
            return Ok();
        }

        public async Task<IActionResult> Rankings(int page = 1)
        {
            RankingsViewModel vm = new RankingsViewModel();

            List<HealthActivity> userActivities = new List<HealthActivity>();
            List<HealthActivity> userActivitiesCombined = new List<HealthActivity>();
            bool activityAlreadyAdded;
            GroupWithMembers groupWithMembers = await GetGroupOfUser();
            List<string> goalMetricStringsList = new List<string>
            {
                "caloriesBurnt",
                "stepsTaken",
                "metresTravelled",
            };
            string[] goalMetricStringArray = goalMetricStringsList.ToArray();

            List<ActivityType> activityTypes = await GetHealthDataActivityTypes();
            // activityOccurences will store how many instances of a certain type of activity are present in the database
            Dictionary<int, int> activityOccurences = new Dictionary<int, int>();
            Dictionary<int, string> activityTypeDict = new Dictionary<int, string>();
            bool renderTables = true;
            foreach (ActivityType type in activityTypes)
            {
                activityTypeDict.Add(type.Id, type.Name);
                activityOccurences.Add(type.Id, 0);
            }

            if (groupWithMembers != null)
            {
                if (activityTypes.Count > 0)
                {
                    for (int i = 0; i < groupWithMembers.Members.Length; i++)
                    {
                        userActivities = await GetUserActivities(groupWithMembers.Members[i].UserId);
                        if (userActivities.Count > 0)
                        {
                            foreach (HealthActivity ha in userActivities)
                            {
                                activityAlreadyAdded = false;
                                foreach (HealthActivity hac in userActivitiesCombined)
                                {
                                    if (hac.activityTypeId.Equals(ha.activityTypeId) && hac.userId.Equals(ha.userId))
                                    {
                                        // do any other checks (probably to do with activity dates) here
                                        hac.caloriesBurnt += ha.caloriesBurnt;
                                        hac.metresTravelled += ha.metresTravelled;
                                        hac.stepsTaken += ha.stepsTaken;
                                        activityAlreadyAdded = true;
                                        hac.metresElevationGained += ha.metresElevationGained;
                                        break;
                                    }
                                    hac.metresElevationGained = Math.Max(0, hac.metresElevationGained);
                                }
                                if (!activityAlreadyAdded) { userActivitiesCombined.Add(ha); }
                                activityOccurences[ha.activityTypeId]++;
                            }
                        }
                        else
                        {
                            vm.Message = "Nobody in your group currently has any activity data, would you like to <a href=" + AppConfig.GetValue<string>("ChallengeUrl") + "userchallenges" + ">make some</a>?";
                            renderTables = false;
                        }
                    }
                    foreach (KeyValuePair<int, int> entry in activityOccurences)
                    {
                        if (entry.Value == 0)
                        {
                            // remove any activity types which aren't featured in the activities list before sending to rankings model
                            activityTypeDict.Remove(entry.Key);
                        }
                    }
                    userActivitiesCombined = userActivitiesCombined.OrderByDescending(h => h.caloriesBurnt).ToList();
                    List<string> userList = new List<string>();
                    foreach (HealthActivity ha in userActivitiesCombined)
                    {
                        userList.Add(ha.userId);
                    }

                    string GetUsersPath = AppConfig.GetValue<string>("GatekeeperUrl") + "api/Users/Batch";
                    var response = await Client.PostAsync(GetUsersPath, userList.Distinct());
                    JArray jsonArrayOfUsers = JArray.Parse(await response.Content.ReadAsStringAsync());
                    foreach (HealthActivity ha in userActivitiesCombined)
                    {
                        foreach (JObject j in jsonArrayOfUsers)
                        {
                            if (ha.userId == j.GetValue("id").ToString())
                            {
                                ha.userId = j.GetValue("email").ToString();
                            }
                        }
                    }

                    vm.ActivityTypeDict = activityTypeDict;
                    vm.Activities = userActivitiesCombined.ToPagedList(page, 20);
                    vm.RenderTables = renderTables;
                    vm.GoalMetrics = goalMetricStringArray;
                }
                else
                {
                    vm.Message = "Nobody in your group currently has any activities, would you like to <a href=" + AppConfig.GetValue<string>("ChallengeUrl") + "userchallenges" + ">make some</a>?";
                    vm.RenderTables = false;
                }
            }
            else
            {
                vm.Message = "You aren't currently a member of a group. Would you like to <a href=" + AppConfig.GetValue<string>("UserGroupsUrl") + ">join one</a>?";
                vm.RenderTables = false;
            }

            // controller: 
            // 1. get the group the current user is in
            // 2. get every member of the group
            // 3. get activity and goal metric data for each group member
            // view: 
            // 4. get desired activity and goal metric from data
            // 5. get total of data per person
            // 6. rank group members by total
            return View(vm);
        }

        public async Task<IActionResult> RemoveData(int page = 1)
        {
            RemoveDataViewModel vm = new RemoveDataViewModel();
            List<HealthActivity> allActivities = await GetUserActivities(User.Claims.FirstOrDefault(c => c.Type == "sub").Value);

            if (allActivities.Count > 0)
            {
                vm.Activities = allActivities.ToPagedList(page, 20);
                vm.ActivityTypes = await GetHealthDataActivityTypes();
                vm.RenderTable = true;
            }
            else
            {
                vm.Message = "You don't have any activities recorded. Would you like to <a href='" + Url.Action("Input", "Home") + "'>record one?</a>";
                vm.RenderTable = false;
            }

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> RemoveDataAjax(int id)
        {
            var response = await DeleteActivity(id);

            if (!response.IsSuccessStatusCode)
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
                var path = AppConfig.GetValue<string>("HealthDataRepositoryUrl") + "api/Activities/" + id;
                return await Client.DeleteAsync(path);
            }

            HttpResponseMessage r = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable
            };
            return r;
        }


        private async Task<HttpResponseMessage> DeleteGoal(int id)
        {
            return await Client.DeleteAsync(AppConfig.GetValue<string>("ChallengeUrl") + "api/challengesManage/" + id);
        }

        private async Task<List<UserChallenge>> GetGroupChallenges()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == "sub").Value;
            var response = await Client.GetAsync(AppConfig.GetValue<string>("ChallengeUrl") + "api/challengesManage/getGroup/" + userId);
            return await response.Content.ReadAsAsync<List<UserChallenge>>();
        }

        private async Task<List<ActivityType>> GetHealthDataActivityTypes()
        {
            string path = AppConfig.GetValue<string>("HealthDataRepositoryUrl") + "api/ActivityTypes";
            var response = await Client.GetAsync(path);
            return await response.Content.ReadAsAsync<List<ActivityType>>();
        }

        private async Task<List<UserChallenge>> GetPersonalGoals()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == "sub").Value;
            var response = await Client.GetAsync(AppConfig.GetValue<string>("ChallengeUrl") + "api/challengesManage/getPersonal/" + userId);
            return await response.Content.ReadAsAsync<List<UserChallenge>>();

        }

        private async Task<List<HealthActivity>> GetUserActivities(string userId, string from = null, string to = null)
        {
            if (from == null ^ to == null)
            {
                throw new ArgumentException("Either both or neither 'from' and 'to' dates must be specified.");
            }

            string path = AppConfig.GetValue<string>("HealthDataRepositoryUrl") + "api/Activities/ByUser/" + (String.IsNullOrEmpty(userId) ? User.Claims.FirstOrDefault(c => c.Type == "sub").Value : userId);
            if (from != null)
            {
                path += "?from=" + from + "&to=" + to;
            }

            var response = await Client.GetAsync(path);
            return await response.Content.ReadAsAsync<List<HealthActivity>>();

        }
        
        private async Task<List<Group>> GetUserGroups()
        {
            var response = await Client.GetAsync(AppConfig.GetValue<string>("UserGroupsUrl") + "api/Groups/");
            return await response.Content.ReadAsAsync<List<Group>>();
        }
        
        private async Task<GroupWithMembers> GetUserGroupWithMembers(int groupId)
        {
            var response = await Client.GetAsync(AppConfig.GetValue<string>("UserGroupsUrl") + "api/Groups/" + groupId);
            return await response.Content.ReadAsAsync<GroupWithMembers>();
        }

        private async Task<List<ChallengeActivityType>> GetValidChallengeActivityTypes()
        {
            string path = AppConfig.GetValue<string>("ChallengeUrl") + "api/activities";
            var response = await Client.GetAsync(path);
            return await response.Content.ReadAsAsync<List<ChallengeActivityType>>();
        }

        private async Task<List<GoalMetric>> GetGoalMetrics()
        {
            var path = AppConfig.GetValue<string>("ChallengeUrl") + "api/goalMetric";
            var response = await Client.GetAsync(path);
            return await response.Content.ReadAsAsync<List<GoalMetric>>();
        }
        
        private async Task<GroupWithMembers> GetGroupOfUser()
        {

            var path = AppConfig.GetValue<string>("UserGroupsUrl") + "api/Groups/ForUser/" + User.Claims.FirstOrDefault(c => c.Type == "sub").Value;
            var response = await Client.GetAsync(path);
            return await response.Content.ReadAsAsync<GroupWithMembers>();
        }

        private async Task<HttpResponseMessage> PostFormActivity()
        {
            // Parsing null string problems
            var activity = new
            {
                userId = User.Claims.FirstOrDefault(c => c.Type == "sub").Value,
                startTimestamp = Request.Form["start-time"],
                endTimestamp = Request.Form["end-time"],
                source = Request.Form["source"],
                activityTypeId = StringValues.IsNullOrEmpty(Request.Form["activity-type"]) ? 0 : int.Parse(Request.Form["activity-type-id"]),
                caloriesBurnt = StringValues.IsNullOrEmpty(Request.Form["calories-burnt"]) ? 0 : int.Parse(Request.Form["calories-burnt"]),
                averageHeartRate = StringValues.IsNullOrEmpty(Request.Form["average-heart-rate"]) ? 0 : int.Parse(Request.Form["average-heart-rate"]),
                stepsTaken = StringValues.IsNullOrEmpty(Request.Form["steps-taken"]) ? 0 : int.Parse(Request.Form["steps-taken"]),
                metresTravelled = StringValues.IsNullOrEmpty(Request.Form["metres-travelled"]) ? 0 : int.Parse(Request.Form["metres-travelled"]),
                metresElevationGained = StringValues.IsNullOrEmpty(Request.Form["metres-elevation-gained"]) ? 0 : int.Parse(Request.Form["metres-elevation-gained"])
            };

            return await Client.PostAsync<object>(AppConfig.GetValue<string>("HealthDataRepositoryUrl") + "activity", activity);
        }

        private async Task<HttpResponseMessage> PostFormGoal()
        {
            var uc = new
            {
                userId = User.Claims.FirstOrDefault(c => c.Type == "sub").Value,
                challenge = new
                {
                    startDateTime = Request.Form["start-time"].ToString(),
                    endDateTime = Request.Form["end-time"].ToString(),
                    goal = int.Parse(Request.Form["target"]),
                    GoalMetricId = int.Parse(Request.Form["goal-metric"]),
                    activityId = int.Parse(Request.Form["activity-type"]),
                },
            };
            return await Client.PostAsync<object>(AppConfig.GetValue<string>("ChallengeUrl") + "api/challengesManage", uc);
        }
    }

    public class GoalsViewModel
    {
        public List<ChallengeActivityType> ActivityTypes { get; set; }
        public List<UserChallenge> Goals { get; set; }
        public List<GoalMetric> GoalMetrics { get; set; }
        public string Message { get; set; }
    }

    public class IndexViewModel
    {
        public Dictionary<string, List<HealthActivity>> Activities { get; set; }
        public List<ActivityType> ActivityTypes { get; set; }
        public List<UserChallenge> Challenges { get; set; }
        public string ChallengeJoinUrl { get; set; }
        public List<UserChallenge> Goals { get; set; }
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
        public string Message { get; set; }
        public bool RenderTable { get; set; }
    }
    public class RankingsViewModel
    {
        public IPagedList<HealthActivity> Activities { get; set; }
        public Dictionary<int, string> ActivityTypeDict { get; set; }
        public string[] GoalMetrics { get; set; }
        public string Message { get; set; }
        public bool RenderTables { get; set; }
    }
}
