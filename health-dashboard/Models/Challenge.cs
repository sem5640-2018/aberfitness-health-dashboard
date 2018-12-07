namespace health_dashboard.Models
{
    public class Challenge
    {
        public int ChallengeId { get; set; }
        public string StartDateTime { get; set; }
        public string EndDateTime { get; set; }
        public int ActivityId { get; set; }
        public ChallengeActivity Activity { get; set; }
        public int PercentComplete { get; set; }
        public bool IsGroupChallenge { get; set; }
        public int Goal { get; set; }
        public bool Repeat { get; set; }
    }

    public class ChallengeActivity
    {
        public int ActivityId { get; set; }
        public string ActivityName { get; set; }
        public string GoalMetric { get; set; }
    }

    public class UserChallenge
    {
        public int UserChallengeId { get; set; }
        public int UserId { get; set; }
        public int ChallengeId { get; set; }
        public int PercentageComplete { get; set; }
    }
}

