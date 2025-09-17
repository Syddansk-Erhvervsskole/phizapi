using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using phizapi.Services;

namespace phizapi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UserController : ControllerBase
    {
        private readonly JwtTokenService _jwt;

        public UserController(JwtTokenService jwt)
        {
            _jwt = jwt;
        }

        [HttpPost("Login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {

            if (request.Username == "admin" && request.Password == "password123")
            {
                var token = _jwt.GenerateToken(request.Username, "test");

                Response.Cookies.Append("access_token", token, new CookieOptions
                {
                    HttpOnly = true,                 // JS cannot access
                    //Secure = true,                   // HTTPS only
                    SameSite = SameSiteMode.Strict,  // Prevent cross-site use
                    Expires = DateTime.UtcNow.AddHours(1)
                });

                return Ok(new { token });
            }

            return Unauthorized(new { message = "Invalid credentials" });
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

    }

    public class LoginRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
