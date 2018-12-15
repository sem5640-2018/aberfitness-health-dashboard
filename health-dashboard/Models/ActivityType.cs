namespace health_dashboard.Models
{
    public class ActivityType
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DataSource Source { get; set; }
    }

    public class DataSource {
        public int Id { get; set; }
        public string[] Mappings { get; set; }
    }

    public class ChallengeActivityType
    {
        public int activityId { get; set; }
        public string activityName { get; set; }
        public int DbActivityId { get; set; }
    }
}
