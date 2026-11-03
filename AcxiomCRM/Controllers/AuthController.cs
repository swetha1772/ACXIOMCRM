using System.Threading.Tasks;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers
{
    public class AuthController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAuditService _auditService;

        public AuthController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditService _auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            this._auditService = _auditService;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Find user by email or username
            var user = await _userManager.FindByEmailAsync(model.EmailOrUsername)
                       ?? await _userManager.FindByNameAsync(model.EmailOrUsername);

            if (user == null)
            {
                await _auditService.LogAsync(
                    userId: "Unknown",
                    userName: model.EmailOrUsername,
                    action: "Failed Login",
                    entityName: "Auth",
                    recordId: null,
                    oldValue: null,
                    newValue: null,
                    details: "Login failed: User not found",
                    result: "Failed"
                );

                ModelState.AddModelError(string.Empty, "Invalid login credentials.");
                return View(model);
            }

            if (!user.IsActive)
            {
                await _auditService.LogAsync(
                    userId: user.Id,
                    userName: user.UserName,
                    action: "Failed Login",
                    entityName: "Auth",
                    recordId: user.Id,
                    oldValue: null,
                    newValue: null,
                    details: "Login rejected: Account is inactive",
                    result: "Failed"
                );

                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact an administrator.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                await _auditService.LogAsync(
                    userId: user.Id,
                    userName: user.UserName,
                    action: "Login",
                    entityName: "Auth",
                    recordId: user.Id,
                    oldValue: null,
                    newValue: null,
                    details: $"User '{user.UserName}' successfully logged in",
                    result: "Success"
                );

                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }
                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                await _auditService.LogAsync(
                    userId: user.Id,
                    userName: user.UserName,
                    action: "Failed Login",
                    entityName: "Auth",
                    recordId: user.Id,
                    oldValue: null,
                    newValue: null,
                    details: $"Account '{user.UserName}' locked due to repeated failed login attempts",
                    result: "Failed"
                );

                ModelState.AddModelError(string.Empty, "This account has been locked due to multiple failed login attempts. Please try again later or contact an administrator.");
                return View(model);
            }

            await _auditService.LogAsync(
                userId: user.Id,
                userName: user.UserName,
                action: "Failed Login",
                entityName: "Auth",
                recordId: user.Id,
                oldValue: null,
                newValue: null,
                details: "Login failed: Incorrect password",
                result: "Failed"
            );

            ModelState.AddModelError(string.Empty, "Invalid login credentials.");
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new RegisterViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "An account with this email address already exists.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                IsActive = true,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                var role = string.IsNullOrEmpty(model.Role) ? "SalesExecutive" : model.Role;
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    role = "SalesExecutive";
                }
                await _userManager.AddToRoleAsync(user, role);

                await _auditService.LogAsync(
                    userId: user.Id,
                    userName: user.UserName,
                    action: "Create",
                    entityName: "User",
                    recordId: user.Id,
                    oldValue: null,
                    newValue: $"Role: {role}",
                    details: $"New user '{user.Email}' registered with role '{role}'",
                    result: "Success"
                );

                TempData["Success"] = "Registration successful! You may now log in.";
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var userId = _userManager.GetUserId(User);
            var userName = User.Identity?.Name;

            await _signInManager.SignOutAsync();

            if (!string.IsNullOrEmpty(userId))
            {
                await _auditService.LogAsync(
                    userId: userId,
                    userName: userName,
                    action: "Logout",
                    entityName: "Auth",
                    recordId: userId,
                    oldValue: null,
                    newValue: null,
                    details: $"User '{userName}' logged out",
                    result: "Success"
                );
            }

            TempData["Success"] = "You have been logged out successfully.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
