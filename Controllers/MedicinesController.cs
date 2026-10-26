using MEDICSALLOCATION_2.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEDICSALLOCATION_2.Controllers;


[Authorize(Roles = "Patient")]
public class MedicinesController : Controller
{
    private readonly ApplicationDbContext _db;
    public MedicinesController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Search(string? q, string? district)
    {
        var today = DateTime.UtcNow.Date;
        var query = _db.InventoryItems
            .Include(i => i.Medicine).Include(i => i.Facility)
            .Where(i => i.Facility.IsActive)
            .Where(i => !i.ExpiryDate.HasValue || i.ExpiryDate.Value.Date >= today)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(i => i.Medicine.Name.Contains(q)
                                  || (i.Medicine.GenericName != null && i.Medicine.GenericName.Contains(q)));

        if (!string.IsNullOrWhiteSpace(district))
            query = query.Where(i => i.Facility.District == district);

        var results = await query
            .OrderByDescending(i => i.QuantityOnHand > 0)
            .ThenBy(i => i.Medicine.Name)
            .ThenBy(i => i.Facility.District)
            .Take(200)
            .ToListAsync();

        ViewBag.Query = q;
        ViewBag.District = district;
        ViewBag.Districts = await _db.Facilities.Select(f => f.District).Distinct().ToListAsync();

        return View(results);
    }
}


