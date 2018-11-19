namespace health_dashboard.Models
{
    public class HealthActivity
    {
        public string start_time { get; set; } // ISO8601 w/o Time Zone
        public string activity_type { get; set; }
        public string distance { get; set; } // Metres
        public string duration { get; set; } // Seconds
        public HealthActivityQuantity quantity { get; set; }

    }

    public class HealthActivityQuantity
    {
        public int value { get; set; }
        public string unit { get; set; }
    }
}
