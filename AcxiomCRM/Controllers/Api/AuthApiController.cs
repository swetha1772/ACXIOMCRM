using System.Linq;
using System.Threading.Tasks;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    public class AuthApiController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public AuthApiController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _auditService = auditService;
        }

        public class ApiLoginRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ApiLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Username and password are required." });
            }

            var user = await _userManager.FindByEmailAsync(request.Username)
                       ?? await _userManager.FindByNameAsync(request.Username);

            if (user == null || !user.IsActive)
            {
                await _auditService.LogAsync("Unknown", request.Username, "Failed Login", "Auth", null, null, null, "API Login Failed: User not found or inactive", result: "Failed");
                return Unauthorized(new { message = "Invalid credentials." });
            }

            var result = await _signInManager.PasswordSignInAsync(user.UserName!, request.Password, isPersistent: false, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                var roles = await _userManager.GetRolesAsync(user);
                await _auditService.LogAsync(user.Id, user.UserName, "Login", "Auth", user.Id, null, null, "API Login Success", result: "Success");

                return Ok(new
                {
                    message = "Login successful",
                    userId = user.Id,
                    email = user.Email,
                    fullName = user.FullName,
                    roles = roles.ToList()
                });
            }

            if (result.IsLockedOut)
            {
                await _auditService.LogAsync(user.Id, user.UserName, "Failed Login", "Auth", user.Id, null, null, "API Login Account Locked", result: "Failed");
                return StatusCode(403, new { message = "Account locked due to repeated failed login attempts." });
            }

            await _auditService.LogAsync(user.Id, user.UserName, "Failed Login", "Auth", user.Id, null, null, "API Login Incorrect password", result: "Failed");
            return Unauthorized(new { message = "Invalid credentials." });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var userId = _userManager.GetUserId(User);
            await _signInManager.SignOutAsync();

            if (!string.IsNullOrEmpty(userId))
            {
                await _auditService.LogAsync(userId, User.Identity?.Name, "Logout", "Auth", userId, null, null, "API Logout", result: "Success");
            }

            return Ok(new { message = "Logged out successfully" });
        }
    }
}
