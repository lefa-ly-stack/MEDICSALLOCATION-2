using MEDICSALLOCATION_2.Models;
using MEDICSALLOCATION_2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MEDICSALLOCATION_2.Controllers;

[Authorize(Roles = "Admin,PharmacyStaff")]
public class AlertsController : Controller
{
    private readonly IAlertService _alerts;
    private readonly UserManager<ApplicationUser> _users;

    public AlertsController(IAlertService alerts, UserManager<ApplicationUser> users)
    {
        _alerts = alerts;
        _users = users;
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

        return View(await _alerts.GetActiveAlertsAsync(facilityId));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id)
    {
        int? facilityId = null;
        if (!User.IsInRole("Admin"))
        {
            var user = await _users.GetUserAsync(User);
            if (user?.FacilityId is not int assignedFacilityId) return Forbid();
            facilityId = assignedFacilityId;
        }

        if (!await _alerts.ResolveAlertAsync(id, facilityId)) return NotFound();
        return RedirectToAction(nameof(Index));
    }
}
