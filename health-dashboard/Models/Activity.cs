namespace health_dashboard.Models
{
    public class HealthActivity
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string StartTimestamp { get; set; } // ISO8601 w/o Time Zone
        public string EndTimestamp { get; set; } // ISO8601 w/o Time Zone
        public int Source { get; set; }
        public int ActivityType { get; set; }
        public int CaloriesBurnt { get; set; }
        public int AverageHeartRate { get; set; }
        public int StepsTaken { get; set; }
        public int MetresTravelled { get; set; }
        public int MetresElevationGained { get; set; }
    }
}

