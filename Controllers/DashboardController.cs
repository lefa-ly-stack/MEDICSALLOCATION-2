using MEDICSALLOCATION_2.Data;
using MEDICSALLOCATION_2.Models;
using MEDICSALLOCATION_2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEDICSALLOCATION_2.Controllers;

[Authorize(Roles = "Admin,PharmacyStaff")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAlertService _alerts;
    private readonly IRedistributionService _redistribution;
    private readonly UserManager<ApplicationUser> _users;

    public DashboardController(ApplicationDbContext db, IAlertService alerts, IRedistributionService redistribution, UserManager<ApplicationUser> users)
    {
        _db = db; _alerts = alerts; _redistribution = redistribution; _users = users;
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

        var inventory = _db.InventoryItems.AsQueryable();
        if (facilityId.HasValue) inventory = inventory.Where(i => i.FacilityId == facilityId.Value);

        ViewBag.TotalMedicines = await _db.Medicines.CountAsync();
        ViewBag.TotalFacilities = facilityId.HasValue ? 1 : await _db.Facilities.CountAsync(f => f.IsActive);
        ViewBag.LowStock = await inventory.CountAsync(i => i.QuantityOnHand <= i.ReorderLevel);
        ViewBag.Overstock = await inventory.CountAsync(i => i.QuantityOnHand > i.MaxStockLevel);
        ViewBag.NearExpiry = await inventory.CountAsync(i =>
            i.ExpiryDate != null && i.ExpiryDate <= DateTime.UtcNow.AddDays(90) && i.QuantityOnHand > 0);
        var alerts = _db.StockAlerts.Where(a => !a.IsResolved);
        if (facilityId.HasValue) alerts = alerts.Where(a => a.FacilityId == facilityId.Value);
        ViewBag.OpenAlerts = await alerts.CountAsync();
        var recommendations = _db.RedistributionRecommendations.Where(r => r.Status == RedistributionStatus.Pending);
        if (facilityId.HasValue)
            recommendations = recommendations.Where(r => r.FromFacilityId == facilityId.Value || r.ToFacilityId == facilityId.Value);
        ViewBag.PendingRecs = await recommendations.CountAsync();

        var availability = await _db.Medicines
            .Select(m => new
            {
                m.Name,
                Total = facilityId.HasValue
                    ? m.InventoryItems.Count(i => i.FacilityId == facilityId.Value)
                    : m.InventoryItems.Count(),
                Available = facilityId.HasValue
                    ? m.InventoryItems.Count(i => i.FacilityId == facilityId.Value && i.QuantityOnHand > 0)
                    : m.InventoryItems.Count(i => i.QuantityOnHand > 0)
            })
            .ToListAsync();

        ViewBag.AvailabilityLabels = availability.Select(a => a.Name).ToList();
        ViewBag.AvailabilityPct = availability.Select(a =>
            a.Total == 0 ? 0 : Math.Round((double)a.Available * 100 / a.Total, 1)).ToList();
        ViewBag.IsAdmin = User.IsInRole("Admin");
        ViewBag.FacilityName = facilityId.HasValue
            ? await _db.Facilities.Where(f => f.Id == facilityId.Value).Select(f => f.Name).FirstOrDefaultAsync()
            : null;

        return View();
    }

    // Regenerate alerts + recommendations on demand
    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> RunScans()
    {
        await _alerts.GenerateAlertsAsync();
        await _redistribution.GenerateRecommendationsAsync();
        TempData["Ok"] = "Alerts and redistribution recommendations refreshed.";
        return RedirectToAction(nameof(Index));
    }

    // CSV export for Power BI
    [HttpGet]
    public async Task<IActionResult> ExportCsv()
    {
        var query = _db.InventoryItems.AsQueryable();
        if (!User.IsInRole("Admin"))
        {
            var user = await _users.GetUserAsync(User);
            if (user?.FacilityId is not int facilityId) return Forbid();
            query = query.Where(i => i.FacilityId == facilityId);
        }

        var items = await query
            .Include(i => i.Medicine).Include(i => i.Facility)
            .Select(i => new {
                Facility = i.Facility.Name,
                District = i.Facility.District,
                Medicine = i.Medicine.Name,
                Category = i.Medicine.Category,
                i.QuantityOnHand,
                i.ReorderLevel,
                i.MaxStockLevel,
                i.ExpiryDate,
                i.LastUpdated
            })
            .ToListAsync();

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Facility,District,Medicine,Category,QuantityOnHand,ReorderLevel,MaxStockLevel,ExpiryDate,LastUpdated");
        foreach (var r in items)
            csv.AppendLine($"{r.Facility},{r.District},{r.Medicine},{r.Category},{r.QuantityOnHand},{r.ReorderLevel},{r.MaxStockLevel},{r.ExpiryDate:yyyy-MM-dd},{r.LastUpdated:yyyy-MM-dd}");

        return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()),
            "text/csv", $"medicsallocation-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}

