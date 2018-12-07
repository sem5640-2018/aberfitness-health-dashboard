namespace health_dashboard.Models
{
    public class Group
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; }
    }

    public class GroupWithMembers
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; }
        public GroupMember[] GroupMembers { get; set; }
    }

    public class GroupMember
    {
        public int UserId { get; set; }
    }
}

