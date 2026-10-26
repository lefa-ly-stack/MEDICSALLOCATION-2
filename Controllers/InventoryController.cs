using MEDICSALLOCATION_2.Data;
using MEDICSALLOCATION_2.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MEDICSALLOCATION_2.Controllers;



[Authorize(Roles = "Admin,PharmacyStaff")]
public class InventoryController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public InventoryController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    // ---------- READ (list) ----------
    public async Task<IActionResult> Index(string? q, int? facilityId)
    {
        var query = _db.InventoryItems
            .Include(i => i.Medicine)
            .Include(i => i.Facility)
            .AsQueryable();

        // Staff only see their own facility unless Admin
        if (!User.IsInRole("Admin"))
        {
            var user = await _users.GetUserAsync(User);
            if (user?.FacilityId is not int fid) return Forbid();
            query = query.Where(i => i.FacilityId == fid);
        }
        else if (facilityId.HasValue)
        {
            query = query.Where(i => i.FacilityId == facilityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(i => i.Medicine.Name.Contains(q));

        ViewBag.Query = q;
        ViewBag.Facilities = new SelectList(
            await _db.Facilities.OrderBy(f => f.Name).ToListAsync(),
            "Id", "Name", facilityId);

        return View(await query.OrderBy(i => i.Medicine.Name).ToListAsync());
    }

    // ---------- READ (details) ----------
    public async Task<IActionResult> Details(int id)
    {
        var item = await _db.InventoryItems
            .Include(i => i.Medicine)
            .Include(i => i.Facility)
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item is null) return NotFound();
        if (!await CanManageItemAsync(item)) return Forbid();
        return View(item);
    }

    // ---------- CREATE (GET) ----------
    public async Task<IActionResult> Create()
    {
        var user = await _users.GetUserAsync(User);
        if (!User.IsInRole("Admin") && user?.FacilityId is not int) return Forbid();
        var facilityId = User.IsInRole("Admin") ? null : user!.FacilityId;
        await PopulateDropdownsAsync(facilityId, facilityScopeId: facilityId);
        return View(new InventoryItem { FacilityId = facilityId ?? 0, ReorderLevel = 20, MaxStockLevel = 300 });
    }

    // ---------- CREATE (POST) ----------
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InventoryItem model)
    {
        var user = await _users.GetUserAsync(User);
        if (!User.IsInRole("Admin"))
        {
            if (user?.FacilityId is not int fid) return Forbid();
            model.FacilityId = fid;
        }

        if (!ModelState.IsValid)
        {
            var facilityScopeId = User.IsInRole("Admin") ? null : user!.FacilityId;
            await PopulateDropdownsAsync(model.FacilityId, model.MedicineId, facilityScopeId);
            return View(model);
        }

        model.LastUpdated = DateTime.UtcNow;
        model.UpdatedByUserId = user?.Id;

        _db.InventoryItems.Add(model);
        await _db.SaveChangesAsync();

        _db.StockTransactions.Add(new StockTransaction
        {
            InventoryItemId = model.Id,
            Type = TransactionType.Received,
            QuantityChange = model.QuantityOnHand,
            Reason = "Initial stock record created",
            UserId = user?.Id
        });
        await _db.SaveChangesAsync();

        TempData["Ok"] = $"Stock record for {model.MedicineId} created.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- UPDATE (GET) ----------
    public async Task<IActionResult> Edit(int id)
    {
        var item = await _db.InventoryItems.Include(i => i.Facility).Include(i => i.Medicine).FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return NotFound();
        if (!await CanManageItemAsync(item)) return Forbid();
        await PopulateDropdownsAsync(item.FacilityId, item.MedicineId, User.IsInRole("Admin") ? null : item.FacilityId);
        return View(item);
    }

    // ---------- UPDATE (POST) ----------
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, InventoryItem model)
    {
        if (id != model.Id) return BadRequest();
        var existing = await _db.InventoryItems.Include(i => i.Facility).Include(i => i.Medicine).FirstOrDefaultAsync(i => i.Id == id);
        if (existing is null) return NotFound();
        if (!await CanManageItemAsync(existing)) return Forbid();
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(existing.FacilityId, existing.MedicineId, User.IsInRole("Admin") ? null : existing.FacilityId);
            return View(existing);
        }

        int delta = model.QuantityOnHand - existing.QuantityOnHand;

        existing.QuantityOnHand = model.QuantityOnHand;
        existing.ReorderLevel = model.ReorderLevel;
        existing.MaxStockLevel = model.MaxStockLevel;
        existing.BatchNumber = model.BatchNumber;
        existing.ExpiryDate = model.ExpiryDate;
        existing.LastUpdated = DateTime.UtcNow;
        existing.UpdatedByUserId = _users.GetUserId(User);

        if (delta != 0)
        {
            _db.StockTransactions.Add(new StockTransaction
            {
                InventoryItemId = existing.Id,
                Type = TransactionType.Adjusted,
                QuantityChange = delta,
                Reason = "Manual adjustment by staff",
                UserId = _users.GetUserId(User)
            });
        }

        await _db.SaveChangesAsync();
        TempData["Ok"] = "Stock record updated.";
        return RedirectToAction(nameof(Index));
    }

    // Record a stock movement and apply its quantity change immediately.
    [HttpGet]
    public async Task<IActionResult> RecordMovement(int id)
    {
        var item = await _db.InventoryItems
            .Include(i => i.Medicine)
            .Include(i => i.Facility)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return NotFound();

        if (!await CanManageItemAsync(item)) return Forbid();

        await PopulateMovementFormAsync(item);
        return View(new InventoryMovementInput());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordMovement(int id, InventoryMovementInput input)
    {
        var item = await _db.InventoryItems
            .Include(i => i.Medicine)
            .Include(i => i.Facility)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return NotFound();

        if (!await CanManageItemAsync(item)) return Forbid();

        var supportedTypes = new[]
        {
            TransactionType.Received,
            TransactionType.Returned,
            TransactionType.Dispensed,
            TransactionType.Expired
        };
        if (!supportedTypes.Contains(input.Type))
            ModelState.AddModelError(nameof(input.Type), "Choose a valid stock movement.");

        if (!ModelState.IsValid)
        {
            await PopulateMovementFormAsync(item);
            return View(input);
        }

        var increasesStock = input.Type is TransactionType.Received or TransactionType.Returned;
        var delta = increasesStock ? input.Quantity : -input.Quantity;
        var userId = _users.GetUserId(User);
        var now = DateTime.UtcNow;

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var stockQuery = _db.InventoryItems.Where(i => i.Id == id);
        if (!increasesStock)
            stockQuery = stockQuery.Where(i => i.QuantityOnHand >= input.Quantity);

        var changedRows = await stockQuery.ExecuteUpdateAsync(update => update
            .SetProperty(i => i.QuantityOnHand, i => i.QuantityOnHand + delta)
            .SetProperty(i => i.LastUpdated, now)
            .SetProperty(i => i.UpdatedByUserId, userId));

        if (changedRows == 0)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(nameof(input.Quantity), "There is not enough stock for this movement.");
            await PopulateMovementFormAsync(item);
            return View(input);
        }

        var defaultReason = input.Type switch
        {
            TransactionType.Received => "Stock received",
            TransactionType.Returned => "Stock returned",
            TransactionType.Dispensed => "Stock dispensed",
            TransactionType.Expired => "Stock expired",
            _ => "Stock movement"
        };
        _db.StockTransactions.Add(new StockTransaction
        {
            InventoryItemId = id,
            Type = input.Type,
            QuantityChange = delta,
            Reason = string.IsNullOrWhiteSpace(input.Reason) ? defaultReason : input.Reason.Trim(),
            UserId = userId,
            Timestamp = now
        });

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Ok"] = $"Inventory updated. New quantity: {item.QuantityOnHand + delta}.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- DELETE (GET: confirm) ----------
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.InventoryItems
            .Include(i => i.Medicine).Include(i => i.Facility)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return NotFound();
        if (!await CanManageItemAsync(item)) return Forbid();
        return View(item);
    }

    // ---------- DELETE (POST) ----------
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await _db.InventoryItems.FindAsync(id);
        if (item is null) return NotFound();
        if (!await CanManageItemAsync(item)) return Forbid();

        _db.InventoryItems.Remove(item);
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Stock record deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync(int? facilityId = null, int? medicineId = null, int? facilityScopeId = null)
    {
        var facilities = _db.Facilities.AsQueryable();
        if (facilityScopeId.HasValue) facilities = facilities.Where(f => f.Id == facilityScopeId.Value);
        ViewBag.Facilities = new SelectList(
            await facilities.OrderBy(f => f.Name).ToListAsync(),
            "Id", "Name", facilityId);

        ViewBag.Medicines = new SelectList(
            await _db.Medicines.OrderBy(m => m.Name)
                .Select(m => new { m.Id, Display = m.Name + " (" + m.Strength + ")" })
                .ToListAsync(),
            "Id", "Display", medicineId);
    }

    private async Task<bool> CanManageItemAsync(InventoryItem item)
    {
        if (User.IsInRole("Admin")) return true;
        var user = await _users.GetUserAsync(User);
        return user?.FacilityId == item.FacilityId;
    }

    private async Task PopulateMovementFormAsync(InventoryItem item)
    {
        ViewBag.InventoryItem = item;
        ViewBag.MovementTypes = new SelectList(new[]
        {
            new { Value = TransactionType.Received, Text = "Received" },
            new { Value = TransactionType.Returned, Text = "Returned" },
            new { Value = TransactionType.Dispensed, Text = "Dispensed" },
            new { Value = TransactionType.Expired, Text = "Expired" }
        }, "Value", "Text");
        await Task.CompletedTask;
    }
}
