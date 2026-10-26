using MEDICSALLOCATION_2.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MEDICSALLOCATION_2.Data;

namespace MEDICSALLOCATION_2.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly ApplicationDbContext _db;

    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, ApplicationDbContext db)
    {
        _users = users;
        _signIn = signIn;
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
    }

    [AllowAnonymous]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _signIn.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
            return Url.IsLocalUrl(model.ReturnUrl)
                ? LocalRedirect(model.ReturnUrl!)
                : RedirectToAction("Index", "Home");

        if (result.IsLockedOut)
            ModelState.AddModelError(string.Empty, "This account is temporarily locked. Please try again later.");
        else if (result.RequiresTwoFactor)
            ModelState.AddModelError(string.Empty, "Two-step sign-in is not configured for this account.");
        else
            ModelState.AddModelError(string.Empty, "The email or password is incorrect.");

        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register() => View(new RegisterAccountViewModel());

    [AllowAnonymous]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterAccountViewModel model)
    {
        var result = await CreateUserAsync(model, "Patient", nameof(Register));
        if (result is not null)
            return result;

        var user = await _users.FindByEmailAsync(model.Email);
        if (user is not null)
            await _signIn.SignInAsync(user, isPersistent: false);

        return RedirectToAction("Index", "Home");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> CreatePharmacist()
    {
        await PopulateFacilitiesAsync();
        return View(new RegisterAccountViewModel());
    }

    [Authorize(Roles = "Admin")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePharmacist(RegisterAccountViewModel model)
    {
        if (model.FacilityId is not int facilityId || !await _db.Facilities.AnyAsync(f => f.Id == facilityId && f.IsActive))
            ModelState.AddModelError(nameof(model.FacilityId), "Choose an active facility for this pharmacist.");

        var result = await CreateUserAsync(model, "PharmacyStaff", nameof(CreatePharmacist), model.FacilityId);
        if (result is not null)
            return result;

        TempData["Success"] = "Pharmacist account created. They can now sign in.";
        return RedirectToAction(nameof(CreatePharmacist));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ManagePharmacists()
    {
        await PopulateFacilitiesAsync();
        return View(await _users.GetUsersInRoleAsync("PharmacyStaff"));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignPharmacistFacility(string userId, int facilityId)
    {
        var staffMember = await _users.FindByIdAsync(userId);
        var facilityExists = await _db.Facilities.AnyAsync(f => f.Id == facilityId && f.IsActive);
        if (staffMember is null || !await _users.IsInRoleAsync(staffMember, "PharmacyStaff") || !facilityExists)
        {
            TempData["Error"] = "Choose a pharmacist account and an active facility.";
            return RedirectToAction(nameof(ManagePharmacists));
        }

        staffMember.FacilityId = facilityId;
        var result = await _users.UpdateAsync(staffMember);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? $"Facility assignment updated for {staffMember.FullName ?? staffMember.Email}."
            : string.Join(" ", result.Errors.Select(e => e.Description));
        return RedirectToAction(nameof(ManagePharmacists));
    }

    private async Task<IActionResult?> CreateUserAsync(RegisterAccountViewModel model, string role, string viewName, int? facilityId = null)
    {
        if (!ModelState.IsValid)
        {
            if (role == "PharmacyStaff") await PopulateFacilitiesAsync();
            return View(viewName, model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            EmailConfirmed = true,
            FacilityId = facilityId
        };

        var createResult = await _users.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            if (role == "PharmacyStaff") await PopulateFacilitiesAsync();
            return View(viewName, model);
        }

        var roleResult = await _users.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            await _users.DeleteAsync(user);
            foreach (var error in roleResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            if (role == "PharmacyStaff") await PopulateFacilitiesAsync();
            return View(viewName, model);
        }

        return null;
    }

    private async Task PopulateFacilitiesAsync()
    {
        ViewBag.Facilities = new SelectList(
            await _db.Facilities.Where(f => f.IsActive).OrderBy(f => f.Name).ToListAsync(),
            "Id", "Name");
    }
}
