using phizapi.Controllers;

namespace phizapi.Models.User
{
    public class User
    {
        public string id { get; set; } = Guid.NewGuid().ToString();
        public string username { get; set; } = "";
        public string password { get; set; } = "";
        public Role role { get; set; } = Role.User;
        public DateTime created_at { get; set; } = DateTime.UtcNow;
        public DateTime updated_at { get; set; } = DateTime.UtcNow;
        public List<CustomData> custom_data { get; set; } = new List<CustomData>();

    }
}
