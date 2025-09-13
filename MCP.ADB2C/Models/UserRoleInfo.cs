namespace MCP.ADB2C.Models
{
    public class UserRoleInfo
    {
        public string UserId { get; set; }
        public string UserDisplayName { get; set; }
        public string UserEmail { get; set; }
        public string ApplicationId { get; set; }
        public string ApplicationName { get; set; }
        public string RoleId { get; set; }
        public string RoleName { get; set; }
        public DateTime AssignedDate { get; set; }
    }
}
