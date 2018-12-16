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

        public async Task<IActionResult> Goals()
        {
            GoalsViewModel vm = new GoalsViewModel();

            if (Request.Method == "POST")
            {
                var response = await PostFormGoal();

                // Add success / failure message
                if (response.IsSuccessStatusCode)
                {
                    vm.Message = "Goal save successful.";
                }
                else
                {
                    vm.Message = "Goal save failed. Please contact a coordinator or an administrator.";
                }
            }

            vm.ActivityTypes = await GetValidChallengeActivityTypes();
            vm.Goals = await GetPersonalGoals();
            vm.GoalMetrics = await GetGoalMetrics();
            vm.HealthDataRepositoryConnectionSuccessful = vm.ActivityTypes != null;

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
            DateTime today = DateTime.Today;
            DateTime weekAgo = DateTime.Today.AddDays(-7);

            IndexViewModel vm = new IndexViewModel
            {
                ActivityTypes = await GetHealthDataActivityTypes(),
                Challenges = await GetGroupChallenges(weekAgo, today),
                ChallengeJoinUrl = AppConfig.GetValue<string>("ChallengeUrl") + "challengesManage",
                Distances = new SortedDictionary<int, double>(),
                Goals = await GetPersonalGoals(weekAgo, today),
                IsFitBitConnected = await GetIsFitBitConnected(),
                FitBitConnectUrl = AppConfig.GetValue<string>("FitBitIngestServiceUrl") + "LoginPage",
                FitBitDisconnectUrl = "https://www.fitbit.com/settings/applications",
            };
            vm.FitBitIngestConnectionSuccessful = vm.IsFitBitConnected == null;

            if (vm.ActivityTypes == null)
            {
                vm.HealthDataRepositoryConnectionSuccessful = false;
            } else
            {
                vm.HealthDataRepositoryConnectionSuccessful = true;

                List<HealthActivity> allActivities = await GetUserActivities(User.Claims.FirstOrDefault(c => c.Type == "sub").Value);
                foreach (HealthActivity ha in allActivities)
                {
                    if (!vm.Distances.ContainsKey(ha.activityTypeId))
                    {
                        vm.Distances.Add(ha.activityTypeId, 0);
                    }
                    vm.Distances[ha.activityTypeId] += ha.metresTravelled;
                }
                List<HealthActivity> apiActivities = await GetUserActivities(User.Claims.FirstOrDefault(c => c.Type == "sub").Value, weekAgo, today);

                SortedDictionary<DateTime, List<HealthActivity>> activitiesByDate = new SortedDictionary<DateTime, List<HealthActivity>>();
                /*
                 *  activitiesByDate = [
                 *      [date] => [
                 *          HealthActivity,
                 *      ],
                 *  ]
                 *
                 **/

                foreach (var a in apiActivities)
                {
                    // Just date - not the time
                    if (!activitiesByDate.ContainsKey(a.startTimestamp.Date))
                    {
                        activitiesByDate.Add(
                            a.startTimestamp.Date,
                            new List<HealthActivity>()
                        );
                    }

                    activitiesByDate[a.startTimestamp.Date].Add(a);
                }

                // Add a zero activity to the data shown by the graph for days with no activity data
                for (DateTime day = weekAgo; day <= today; day = day.AddDays(1)) {
                    if (!activitiesByDate.ContainsKey(day.Date))
                    {
                        activitiesByDate.Add(
                            day.Date,
                            new List<HealthActivity>()
                        );
                        activitiesByDate[day.Date].Add(
                            new HealthActivity
                            {
                                caloriesBurnt = 0,
                                averageHeartRate = 0,
                                stepsTaken = 0,
                                metresTravelled = 0,
                                metresElevationGained = 0
                            }
                        );
                    }
                }

                vm.ActivitiesByDate = activitiesByDate;

                vm.ChallengesConnectionSuccessful = vm.Challenges != null;
            }

            return View(vm);
        }

        public async Task<IActionResult> Input()
        {
            InputViewModel vm = new InputViewModel
            {
                ActivityTypes = await GetHealthDataActivityTypes()
            };
            vm.HealthDataRepositoryConnectionSuccessful = vm.ActivityTypes != null;

            if (vm.HealthDataRepositoryConnectionSuccessful)
            {
                if (Request.Method == "POST")
                {
                    var response = await PostFormActivity();

                    // Add success / failure message
                    if (response.IsSuccessStatusCode)
                    {
                        vm.Message = "Activity data saved successfully.";
                    }
                    else
                    {
                        vm.Message = "Activity data save failed. Please contact a coordinator or an administrator.";
                    }
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
                return Ok();
            }
            return BadRequest(response.Content.ReadAsStringAsync());
        }

        public async Task<IActionResult> Rankings(int page = 1)
        {
            RankingsViewModel vm = new RankingsViewModel();

            List<HealthActivity> userActivities = new List<HealthActivity>();
            List<HealthActivity> userActivitiesCombined = new List<HealthActivity>();
            bool activityAlreadyAdded;
            GroupWithMembers groupWithMembers = await GetGroupOfCurrentUser();
            List<string> goalMetricStringsList = new List<string>
            {
                "caloriesBurnt",
                "stepsTaken",
                "metresTravelled",
            };
            string[] goalMetricStringArray = goalMetricStringsList.ToArray();

            List<ActivityType> activityTypes = await GetHealthDataActivityTypes();
            if (activityTypes != null)
            {
                // activityOccurences will store how many instances of a certain type of activity are present in the database
                Dictionary<int, int> activityOccurences = new Dictionary<int, int>();
                Dictionary<int, string> activityTypeDict = new Dictionary<int, string>();
                bool renderTables = true;
                foreach (ActivityType type in activityTypes)
                {
                    activityTypeDict.Add(type.Id, type.Name);
                    activityOccurences.Add(type.Id, 0);
                }

                int totalActivityKey = activityTypeDict.Last().Key + 1;
                activityTypeDict.TryAdd(totalActivityKey, "All (Total)");
                activityOccurences.TryAdd(totalActivityKey, 0);


                if (groupWithMembers != null)
                {
                    if (activityTypes.Count > 0)
                    {
                        for (int i = 0; i < groupWithMembers.Members.Length; i++)
                        {
                            userActivities = await GetUserActivities(groupWithMembers.Members[i].UserId);
                            if (userActivities.Count > 0)
                            {
                                // append extra activity to each user's list of activities which contains combined total of all user's activities
                                HealthActivity healthActivityTotal = new HealthActivity
                                {
                                    activityTypeId = totalActivityKey,
                                    id = 0,
                                    userId = groupWithMembers.Members[i].UserId,
                                    averageHeartRate = 0,
                                    caloriesBurnt = 0,
                                    stepsTaken = 0,
                                    metresElevationGained = 0,
                                    metresTravelled = 0,
                                    startTimestamp = DateTime.Today,
                                    endTimestamp = DateTime.Today.AddDays(1),
                                    source = "Manual",
                                };
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
                                    healthActivityTotal.caloriesBurnt += ha.caloriesBurnt;
                                    healthActivityTotal.metresTravelled += ha.metresTravelled;
                                    healthActivityTotal.stepsTaken += ha.stepsTaken;
                                    healthActivityTotal.metresElevationGained += ha.metresElevationGained;
                                    if (!activityAlreadyAdded) { userActivitiesCombined.Add(ha); }
                                    activityOccurences[ha.activityTypeId]++;
                                }
                                activityOccurences[totalActivityKey] = 1;
                                userActivitiesCombined.Add(healthActivityTotal);
                            }
                        }
                        if(userActivitiesCombined.Count == 0)
                        {
                            vm.Message = "Nobody in your group currently has any activity data, would you like to <a href=" + AppConfig.GetValue<string>("ChallengeUrl") + "userchallenges" + ">make some</a>?";
                            renderTables = false;
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

                vm.HealthDataRepositoryConnectionSuccessful = true;
            } else
            {
                vm.HealthDataRepositoryConnectionSuccessful = false;
            }

            return View(vm);
        }

        public async Task<IActionResult> RemoveData(int page = 1)
        {
            RemoveDataViewModel vm = new RemoveDataViewModel();
            List<HealthActivity> allActivities = await GetUserActivities(User.Claims.FirstOrDefault(c => c.Type == "sub").Value);

            if (allActivities != null)
            {
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

                vm.HealthDataRepositoryConnectionSuccessful = true;
            } else
            {
                vm.HealthDataRepositoryConnectionSuccessful = false;
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
            return await Client.DeleteAsync(AppConfig.GetValue<string>("HealthDataRepositoryUrl") + "api/Activities/" + id);
        }


        private async Task<HttpResponseMessage> DeleteGoal(int id)
        {
            return await Client.DeleteAsync(AppConfig.GetValue<string>("ChallengeUrl") + "api/challengesManage/" + id);
        }

        private async Task<List<UserChallenge>> GetGroupChallenges(DateTime? from = null, DateTime? to = null)
        {
            if (from == null ^ to == null)
            {
                throw new ArgumentException("Either both or neither 'from' and 'to' dates must be specified.");
            }

            var userId = User.Claims.FirstOrDefault(c => c.Type == "sub").Value;
            var response = await Client.GetAsync(AppConfig.GetValue<string>("ChallengeUrl") + "api/challengesManage/getGroup/" + userId);
            if (response.IsSuccessStatusCode)
            {
                List<UserChallenge> apiChallenges = await response.Content.ReadAsAsync<List<UserChallenge>>();

                List<UserChallenge> challenges = new List<UserChallenge>();
                foreach (var challenge in apiChallenges)
                {
                    if (challenge.challenge.startDateTime <= DateTime.Now && DateTime.Now < challenge.challenge.endDateTime)
                    {
                        challenges.Add(challenge);
                    }
                }
                return challenges;
            }
            return null;
        }

        private async Task<List<ActivityType>> GetHealthDataActivityTypes()
        {
            var response = await Client.GetAsync(AppConfig.GetValue<string>("HealthDataRepositoryUrl") + "api/ActivityTypes");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsAsync<List<ActivityType>>();
            }
            return null;
        }

        private async Task<bool?> GetIsFitBitConnected()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == "sub").Value;
            var response = await Client.GetAsync(AppConfig.GetValue<string>("FitBitIngestServiceUrl") + "api/Check?userId=" + userId);
            if (response.IsSuccessStatusCode)
            {
                return (int)response.StatusCode == 200;
            }
            return null;
        }

        private async Task<List<UserChallenge>> GetPersonalGoals(DateTime? from = null, DateTime? to = null)
        {
            if (from == null ^ to == null)
            {
                throw new ArgumentException("Either both or neither 'from' and 'to' dates must be specified.");
            }

            var userId = User.Claims.FirstOrDefault(c => c.Type == "sub").Value;
            var response = await Client.GetAsync(AppConfig.GetValue<string>("ChallengeUrl") + "api/challengesManage/getPersonal/" + userId);
            if (response.IsSuccessStatusCode)
            {
                List<UserChallenge> apiGoals = await response.Content.ReadAsAsync<List<UserChallenge>>();

                List<UserChallenge> goals = new List<UserChallenge>();
                foreach (var goal in apiGoals)
                {
                    if (goal.challenge.startDateTime <= DateTime.Now && DateTime.Now < goal.challenge.endDateTime)
                    {
                        goals.Add(goal);
                    }
                }
                return goals;
            }
            return null;
        }

        private async Task<List<HealthActivity>> GetUserActivities(string userId, DateTime? from = null, DateTime? to = null)
        {
            if (from == null ^ to == null)
            {
                throw new ArgumentException("Either both or neither 'from' and 'to' dates must be specified.");
            }

            string path = AppConfig.GetValue<string>("HealthDataRepositoryUrl") + "api/Activities/ByUser/" +  userId;
            if (from != null)
            {
                path += "?from=" + from.Value.ToString("yyyy-MM-dd") + "&to=" + to.Value.ToString("yyyy-MM-dd");
            }

            var response = await Client.GetAsync(path);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsAsync<List<HealthActivity>>();
            }
            return null;

        }
        
        private async Task<List<Group>> GetUserGroups()
        {
            var response = await Client.GetAsync(AppConfig.GetValue<string>("UserGroupsUrl") + "api/Groups/");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsAsync<List<Group>>();
            }
            return null;
        }
        
        private async Task<GroupWithMembers> GetUserGroupWithMembers(int groupId)
        {
            var response = await Client.GetAsync(AppConfig.GetValue<string>("UserGroupsUrl") + "api/Groups/" + groupId);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsAsync<GroupWithMembers>();
            }
            return null;
        }

        private async Task<List<ChallengeActivityType>> GetValidChallengeActivityTypes()
        {
            var response = await Client.GetAsync(AppConfig.GetValue<string>("ChallengeUrl") + "api/activities");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsAsync<List<ChallengeActivityType>>();
            }
            return null;
        }

        private async Task<List<GoalMetric>> GetGoalMetrics()
        {
            var response = await Client.GetAsync(AppConfig.GetValue<string>("ChallengeUrl") + "api/goalMetric");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsAsync<List<GoalMetric>>();
            }
            return null;
        }
        
        private async Task<GroupWithMembers> GetGroupOfCurrentUser()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == "sub").Value;
            var response = await Client.GetAsync(AppConfig.GetValue<string>("UserGroupsUrl") + "api/Groups/ForUser/" + userId);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsAsync<GroupWithMembers>();
            }
            return null;
        }

        private async Task<HttpResponseMessage> PostFormActivity()
        {
            // Parsing null string problems
            var activity = new
            {
                userId = User.Claims.FirstOrDefault(c => c.Type == "sub").Value,
                startTimestamp = Request.Form["start-time"].ToString(),
                endTimestamp = Request.Form["end-time"].ToString(),
                source = "Manual",
                activityTypeId = StringValues.IsNullOrEmpty(Request.Form["activity-type"]) ? 0 : int.Parse(Request.Form["activity-type"]),
                caloriesBurnt = StringValues.IsNullOrEmpty(Request.Form["calories-burnt"]) ? 0 : int.Parse(Request.Form["calories-burnt"]),
                averageHeartRate = StringValues.IsNullOrEmpty(Request.Form["average-heart-rate"]) ? 0 : int.Parse(Request.Form["average-heart-rate"]),
                stepsTaken = StringValues.IsNullOrEmpty(Request.Form["steps-taken"]) ? 0 : int.Parse(Request.Form["steps-taken"]),
                metresTravelled = StringValues.IsNullOrEmpty(Request.Form["metres-travelled"]) ? 0 : double.Parse(Request.Form["metres-travelled"]),
                metresElevationGained = StringValues.IsNullOrEmpty(Request.Form["metres-elevation-gained"]) ? 0 : double.Parse(Request.Form["metres-elevation-gained"])
            };

            return await Client.PostAsync<object>(AppConfig.GetValue<string>("HealthDataRepositoryUrl") + "/api/activities", activity);
        }

        private async Task<HttpResponseMessage> PostFormGoal()
        {
            var uc = new
            {
                userId = User.Claims.FirstOrDefault(c => c.Type == "sub").Value,
                challenge = new
                {
                    startDateTime = DateTime.Parse(Request.Form["start-time"]),
                    endDateTime = DateTime.Parse(Request.Form["end-time"]),
                    goal = double.Parse(Request.Form["target"]),
                    GoalMetricId = int.Parse(Request.Form["goal-metric"]),
                    activityId = int.Parse(Request.Form["activity-type"]),
                },
            };
            return await Client.PostAsync<object>(AppConfig.GetValue<string>("ChallengeUrl") + "api/challengesManage", uc);
        }
    }

    public class GoalsViewModel
    { 
        public bool HealthDataRepositoryConnectionSuccessful { get; set; }
        public List<ChallengeActivityType> ActivityTypes { get; set; }
        public List<UserChallenge> Goals { get; set; }
        public List<GoalMetric> GoalMetrics { get; set; }
        public string Message { get; set; }
    }

    public class IndexViewModel
    {
        public bool HealthDataRepositoryConnectionSuccessful { get; set; }
        public bool ChallengesConnectionSuccessful { get; set; }
        public bool FitBitIngestConnectionSuccessful { get; set; }
        public SortedDictionary<DateTime, List<HealthActivity>> ActivitiesByDate { get; set; }
        public SortedDictionary<int, double> Distances { get; set; }
        public List<ActivityType> ActivityTypes { get; set; }
        public List<UserChallenge> Challenges { get; set; }
        public string ChallengeJoinUrl { get; set; }
        public List<UserChallenge> Goals { get; set; }
        public bool? IsFitBitConnected { get; set; }
        public string FitBitConnectUrl { get; set; }
        public string FitBitDisconnectUrl { get; set; }
    }

    public class InputViewModel
    {
        public bool HealthDataRepositoryConnectionSuccessful { get; set; }
        public List<ActivityType> ActivityTypes { get; set; }
        public string Message { get; set; }
    }

    public class RemoveDataViewModel
    {
        public bool HealthDataRepositoryConnectionSuccessful { get; set; }
        public IPagedList<HealthActivity> Activities { get; set; }
        public List<ActivityType> ActivityTypes { get; set; }
        public string Message { get; set; }
        public bool RenderTable { get; set; }
    }
    public class RankingsViewModel
    {
        public bool HealthDataRepositoryConnectionSuccessful { get; set; }
        public IPagedList<HealthActivity> Activities { get; set; }
        public Dictionary<int, string> ActivityTypeDict { get; set; }
        public string[] GoalMetrics { get; set; }
        public string Message { get; set; }
        public bool RenderTables { get; set; }
    }
}
