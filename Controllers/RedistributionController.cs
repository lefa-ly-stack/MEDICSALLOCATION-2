using MEDICSALLOCATION_2.Models;
using MEDICSALLOCATION_2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MEDICSALLOCATION_2.Controllers;



[Authorize(Roles = "Admin,PharmacyStaff")]
public class RedistributionController : Controller
{
    private readonly IRedistributionService _svc;
    private readonly UserManager<ApplicationUser> _users;

    public RedistributionController(IRedistributionService svc, UserManager<ApplicationUser> users)
    {
        _svc = svc; _users = users;
    }

    public async Task<IActionResult> Index()
    {
        int? facilityId = null;
        if (!User.IsInRole("Admin"))
        {
            var user = await _users.GetUserAsync(User);
            if (user?.FacilityId is not int assignedFacilityId) return Forbid();
            facilityId = assignedFacilityId;
        }
        var list = await _svc.GetRecommendationsAsync(facilityId);
        return View(list);
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate()
    {
        await _svc.GenerateRecommendationsAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, RedistributionStatus status)
    {
        if (!Enum.IsDefined(status) || status == RedistributionStatus.Pending) return BadRequest();
        var userId = _users.GetUserId(User) ?? "unknown";
        if (!await _svc.UpdateStatusAsync(id, status, userId))
            TempData["Error"] = "The recommendation could not be updated. Check its current status and available stock.";
        return RedirectToAction(nameof(Index));
    }
}

