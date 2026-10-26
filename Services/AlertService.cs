using MEDICSALLOCATION_2.Data;
using MEDICSALLOCATION_2.Models;
using Microsoft.EntityFrameworkCore;

namespace MEDICSALLOCATION_2.Services;

public class AlertService : IAlertService
{
    private readonly ApplicationDbContext _db;
    private const int NearExpiryDays = 90;

    public AlertService(ApplicationDbContext db) => _db = db;

    public async Task<int> GenerateAlertsAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var nearExpiryCutoff = today.AddDays(NearExpiryDays);

        var items = await _db.InventoryItems
            .Include(i => i.Medicine).Include(i => i.Facility)
            .ToListAsync(ct);

        int created = 0;

        foreach (var item in items)
        {
            if (item.QuantityOnHand == 0)
                created += await AddIfNotExists(item, AlertType.LowStock, AlertSeverity.Critical,
                    $"{item.Medicine.Name} is OUT OF STOCK at {item.Facility.Name}.", ct);
            else if (item.QuantityOnHand <= item.ReorderLevel)
                created += await AddIfNotExists(item, AlertType.LowStock, AlertSeverity.Warning,
                    $"{item.Medicine.Name} at {item.Facility.Name} is low ({item.QuantityOnHand}, reorder {item.ReorderLevel}).", ct);

            if (item.ExpiryDate.HasValue && item.QuantityOnHand > 0)
            {
                if (item.ExpiryDate.Value.Date < today)
                    created += await AddIfNotExists(item, AlertType.Expired, AlertSeverity.Critical,
                        $"{item.Medicine.Name} (batch {item.BatchNumber}) EXPIRED on {item.ExpiryDate:yyyy-MM-dd} at {item.Facility.Name}.", ct);
                else if (item.ExpiryDate.Value.Date <= nearExpiryCutoff)
                    created += await AddIfNotExists(item, AlertType.NearExpiry, AlertSeverity.Warning,
                        $"{item.Medicine.Name} (batch {item.BatchNumber}) expires {item.ExpiryDate:yyyy-MM-dd} at {item.Facility.Name}.", ct);
            }

            if (item.QuantityOnHand > item.MaxStockLevel)
                created += await AddIfNotExists(item, AlertType.Overstock, AlertSeverity.Info,
                    $"{item.Medicine.Name} at {item.Facility.Name} is overstocked ({item.QuantityOnHand} vs max {item.MaxStockLevel}).", ct);
        }

        if (created > 0) await _db.SaveChangesAsync(ct);
        return created;
    }

    private async Task<int> AddIfNotExists(InventoryItem item, AlertType type,
        AlertSeverity sev, string msg, CancellationToken ct)
    {
        bool exists = await _db.StockAlerts.AnyAsync(a =>
            a.FacilityId == item.FacilityId &&
            a.MedicineId == item.MedicineId &&
            a.Type == type && !a.IsResolved, ct);

        if (exists) return 0;

        _db.StockAlerts.Add(new StockAlert
        {
            FacilityId = item.FacilityId,
            MedicineId = item.MedicineId,
            Type = type,
            Severity = sev,
            Message = msg
        });
        return 1;
    }

    public async Task<List<StockAlert>> GetActiveAlertsAsync(int? facilityId = null)
    {
        var q = _db.StockAlerts
            .Include(a => a.Medicine).Include(a => a.Facility)
            .Where(a => !a.IsResolved);

        if (facilityId.HasValue) q = q.Where(a => a.FacilityId == facilityId.Value);

        return await q.OrderByDescending(a => a.Severity)
                      .ThenByDescending(a => a.CreatedAt).ToListAsync();
    }

    public async Task<bool> ResolveAlertAsync(int alertId, int? facilityId = null)
    {
        var query = _db.StockAlerts.AsQueryable();
        if (facilityId.HasValue) query = query.Where(a => a.FacilityId == facilityId.Value);
        var a = await query.FirstOrDefaultAsync(a => a.Id == alertId);
        if (a is null) return false;
        a.IsResolved = true;
        a.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }
}

