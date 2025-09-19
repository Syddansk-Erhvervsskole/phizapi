using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using phizapi.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json.Serialization;
using XSystem.Security.Cryptography;

namespace phizapi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UserController : ControllerBase
    {
        private readonly EncryptionService _es;

        private readonly JwtTokenService _jwt;

        public UserController(JwtTokenService jwt, EncryptionService es)
        {
            _jwt = jwt;
            _es = es;
        }

        [HttpGet("List")]
        public IActionResult GetAll()
        {

            return Ok(MongoDBService.GetList<UserList>("Users"));
        }

        [HttpPost("Login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            try
            {
                var sha256Password = _es.Sha256(request.password);

                var mongouser = MongoDBService.GetList<User>("Users").FirstOrDefault(x => x.username == request.username && x.password == sha256Password);
                if (mongouser != null)
                {
                    var token = _jwt.GenerateToken(mongouser.username, mongouser.role.ToString());

                    Response.Cookies.Append("access_token", token, new CookieOptions
                    {
                        HttpOnly = true,                 // JS cannot access
                        //Secure = true,                   // HTTPS only
                        SameSite = SameSiteMode.Strict,  // Prevent cross-site use
                        Expires = DateTime.UtcNow.AddHours(1)
                    });

                    return Ok(new { token });
                }
            }
            catch(Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
         

            return Unauthorized(new { message = "Invalid credentials" });
        }

        [HttpPost("Register")]
        public IActionResult Register([FromBody] LoginRequest request)
        {
            try
            {
                var mongoUsers = MongoDBService.GetList<User>("Users");

            
                if(mongoUsers.Any(u => u.username == request.username))
                {
                    return Conflict(new { message = "Username already exists" });
                }

                var sha256Password = _es.Sha256(request.password);

                var user = new User()
                {
                    username = request.username,
                    password = sha256Password
                };

                MongoDBService.Upload<User>(user, "Users");

                return Ok(new { message = "User registered successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex });
            }

        }

    

        [HttpPost("Verify")]
        [Authorize]
        public IActionResult Verify()
        {
            return Ok(new { message = "Token is valid" });
        }

        [HttpPost("Logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("access_token");
            return Ok("Logged out");
        }

        [HttpPatch("Role")]
        [Authorize(Roles = "Admin")]
        public IActionResult UpdateRole([FromBody] UserUpdate<Role> update)
        {
            try
            {
                MongoDBService.GetCollection<User>("Users").UpdateOne(X => X.id == update.user_id, Builders<User>.Update
                .Set(u => u.role, update.value));


                return Ok(new { message = "User role updated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex });
            }

        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public IActionResult DeleteUser(string id)
        {
            try
            {

                MongoDBService.GetCollection<User>("Users").DeleteOne(X => X.id == id);


                return Ok(new { message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex });
            }

        }
    }


    public class LoginRequest
    {
        public string username { get; set; } = "";
        public string password { get; set; } = "";
    }

    public class UserUpdate<T>
    {
        public string  user_id { get; set; }

        public T value { get; set; }

        public DateTime updated_at = DateTime.Now;
    }

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

    [BsonIgnoreExtraElements]
    public class UserList
    {
        public string id { get; set; }
        public string username { get; set; }
        public Role role { get; set; }
    }

    public enum Role
    {
        Admin,
        User,
    }
    public class CustomData
    {
        string key{ get; set; } = "";
        object value{ get; set; }
    }
}
