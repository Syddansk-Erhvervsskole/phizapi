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
        private readonly MongoDBService _dBService;

        public UserController(JwtTokenService jwt, EncryptionService es, MongoDBService dBService)
        {
            _jwt = jwt;
            _es = es;
            _dBService = dBService;
        }

        [HttpGet("List")]
        [Authorize(Roles = "Admin")]
        public IActionResult GetAll()
        {

            return Ok(_dBService.GetList<UserList>("Users"));
        }

        [HttpPost("Login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            try
            {
                var sha256Password = _es.Sha256(request.password);

                var mongouser = _dBService.GetList<User>("Users").FirstOrDefault(x => x.username == request.username && x.password == sha256Password);
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
        [Authorize(Roles = "Admin")]
        public IActionResult Register([FromBody] LoginRequest request)
        {
            try
            {
                var mongoUsers = _dBService.GetList<User>("Users");

            
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

                _dBService.Upload<User>(user, "Users");

                return Ok(new { message = "User registered successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex });
            }

        }

    

        [HttpPost("Verify")]
        [Authorize(Roles = "Admin")]
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
                _dBService.GetCollection<User>("Users").UpdateOne(X => X.id == update.user_id, Builders<User>.Update
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

                _dBService.GetCollection<User>("Users").DeleteOne(X => X.id == id);


                return Ok(new { message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex });
            }

        }
    }


 


 


}
