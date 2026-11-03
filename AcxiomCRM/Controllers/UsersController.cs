using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAuditService _auditService;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditService auditService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _auditService = auditService;
        }

        // GET: Users
        public async Task<IActionResult> Index(string? searchTerm, string? roleFilter)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(u =>
                    u.FullName.ToLower().Contains(term) ||
                    (u.Email != null && u.Email.ToLower().Contains(term)));
            }

            var users = await query.OrderBy(u => u.FullName).ToListAsync();
            var userList = new List<UserItemViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault() ?? "None";

                if (!string.IsNullOrWhiteSpace(roleFilter) && role != roleFilter)
                {
                    continue;
                }

                userList.Add(new UserItemViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email ?? string.Empty,
                    Role = role,
                    IsActive = user.IsActive,
                    IsLockedOut = await _userManager.IsLockedOutAsync(user),
                    AccessFailedCount = user.AccessFailedCount,
                    CreatedDate = user.CreatedDate
                });
            }

            var vm = new UserListViewModel
            {
                SearchTerm = searchTerm,
                RoleFilter = roleFilter,
                Users = userList
            };

            return View(vm);
        }

        // GET: Users/Permissions
        public IActionResult Permissions()
        {
            return View();
        }

        // GET: Users/Create
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            var vm = new UserCreateViewModel
            {
                Role = "SalesExecutive",
                RolesList = await GetRolesSelectListAsync()
            };
            return View(vm);
        }

        // POST: Users/Create
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.RolesList = await GetRolesSelectListAsync(model.Role);
                return View(model);
            }

            var existing = await _userManager.FindByEmailAsync(model.Email);
            if (existing != null)
            {
                ModelState.AddModelError("Email", "A user with this email already exists.");
                model.RolesList = await GetRolesSelectListAsync(model.Role);
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email.Trim().ToLower(),
                Email = model.Email.Trim().ToLower(),
                FullName = model.FullName.Trim(),
                IsActive = true,
                EmailConfirmed = true,
                CreatedDate = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                if (await _roleManager.RoleExistsAsync(model.Role))
                {
                    await _userManager.AddToRoleAsync(user, model.Role);
                }

                var currentUserId = _userManager.GetUserId(User);
                await _auditService.LogAsync(
                    userId: currentUserId ?? "Admin",
                    userName: User.Identity?.Name,
                    action: "Create",
                    entityName: "User",
                    recordId: user.Id,
                    oldValue: null,
                    newValue: $"User: {user.Email}, Role: {model.Role}",
                    details: $"Administrator created user '{user.Email}' with role '{model.Role}'",
                    result: "Success"
                );

                TempData["Success"] = $"User '{user.FullName}' created successfully.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            model.RolesList = await GetRolesSelectListAsync(model.Role);
            return View(model);
        }

        // GET: Users/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var currentRole = roles.FirstOrDefault() ?? "SalesExecutive";

            var vm = new UserEditViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Role = currentRole,
                IsActive = user.IsActive,
                RolesList = await GetRolesSelectListAsync(currentRole)
            };

            return View(vm);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, UserEditViewModel model)
        {
            if (id != model.Id) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                model.RolesList = await GetRolesSelectListAsync(model.Role);
                return View(model);
            }

            var currentUserId = _userManager.GetUserId(User);
            var oldRoles = await _userManager.GetRolesAsync(user);
            var oldRole = oldRoles.FirstOrDefault() ?? "None";

            user.FullName = model.FullName.Trim();
            user.IsActive = model.IsActive;

            // Unlock account if requested
            if (model.UnlockAccount)
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                await _userManager.ResetAccessFailedCountAsync(user);
                await _auditService.LogAsync(
                    userId: currentUserId ?? "Admin",
                    userName: User.Identity?.Name,
                    action: "Security",
                    entityName: "User",
                    recordId: user.Id,
                    oldValue: "Locked",
                    newValue: "Unlocked",
                    details: $"Administrator unlocked account '{user.Email}'",
                    result: "Success"
                );
            }

            // Update role if changed
            if (oldRole != model.Role)
            {
                if (oldRoles.Any())
                {
                    await _userManager.RemoveFromRolesAsync(user, oldRoles);
                }
                await _userManager.AddToRoleAsync(user, model.Role);

                await _auditService.LogAsync(
                    userId: currentUserId ?? "Admin",
                    userName: User.Identity?.Name,
                    action: "Role Change",
                    entityName: "User",
                    recordId: user.Id,
                    oldValue: oldRole,
                    newValue: model.Role,
                    details: $"Role for user '{user.Email}' changed from '{oldRole}' to '{model.Role}'",
                    result: "Success"
                );
            }

            // Reset password if provided
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var passResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                if (passResult.Succeeded)
                {
                    await _auditService.LogAsync(
                        userId: currentUserId ?? "Admin",
                        userName: User.Identity?.Name,
                        action: "Security",
                        entityName: "User",
                        recordId: user.Id,
                        oldValue: null,
                        newValue: "Password Reset",
                        details: $"Password for user '{user.Email}' reset by Administrator",
                        result: "Success"
                    );
                }
                else
                {
                    foreach (var error in passResult.Errors)
                    {
                        ModelState.AddModelError("NewPassword", error.Description);
                    }
                    model.RolesList = await GetRolesSelectListAsync(model.Role);
                    return View(model);
                }
            }

            await _userManager.UpdateAsync(user);

            TempData["Success"] = $"User '{user.FullName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<SelectList> GetRolesSelectListAsync(string? selectedRole = null)
        {
            var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync();
            return new SelectList(roles, "Name", "Name", selectedRole);
        }
    }
}
