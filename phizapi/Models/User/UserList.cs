using MongoDB.Bson.Serialization.Attributes;
using phizapi.Controllers;

namespace phizapi.Models.User
{
    [BsonIgnoreExtraElements]
    public class UserList
    {
        public string id { get; set; }
        public string username { get; set; }
        public Role role { get; set; }
    }
}
