namespace health_dashboard.Models
{
    public class Group
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class GroupWithMembers
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public GroupMember[] Members { get; set; }
    }

    public class GroupMember
    {
        public string UserId { get; set; }
    }
}