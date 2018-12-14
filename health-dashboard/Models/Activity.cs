using System;

namespace health_dashboard.Models
{
    public class HealthActivity
    {
        public int id { get; set; }
        public string userId { get; set; }
        public DateTime startTimestamp { get; set; }
        public DateTime endTimestamp { get; set; }
        public string source { get; set; }
        public int activityTypeId { get; set; }
        public int caloriesBurnt { get; set; }
        public int averageHeartRate { get; set; }
        public int stepsTaken { get; set; }
        public double metresTravelled { get; set; }
        public double metresElevationGained { get; set; }
    }
}

