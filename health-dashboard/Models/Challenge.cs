using System;

namespace health_dashboard.Models
{
    public class Challenge
    {
        public int? challengeId { get; set; }
        public DateTime startDateTime { get; set; }
        public DateTime endDateTime { get; set; }
        public double goal { get; set; }
        public GoalMetric GoalMetric { get; set; }
        public int GoalMetricId { get; set; }
        public int activityId { get; set; }
        public ChallengeActivity activity { get; set; }
        public bool repeat { get; set; }
        public bool isGroupChallenge { get; set; }
        public int? groupId { get; set; }
    }

    public class ChallengeActivity
    {
        public int activityId { get; set; }
        public string activityName { get; set; }
    }

    public class UserChallenge
    {
        public int? userChallengeId { get; set; }
        public string userId { get; set; }
        public Challenge challenge { get; set; }
        public int? percentageComplete { get; set; }
    }

    public class GoalMetric
    {
        public int? GoalMetricId { get; set; }
        public string GoalMetricDisplay { get; set; }
        public string GoalMetricDbName { get; set; }
    }
}

