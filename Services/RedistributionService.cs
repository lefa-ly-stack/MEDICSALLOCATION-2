using MEDICSALLOCATION_2.Data;
using MEDICSALLOCATION_2.Models;
using Microsoft.EntityFrameworkCore;

namespace MEDICSALLOCATION_2.Services;

public class RedistributionService : IRedistributionService
{
    private readonly ApplicationDbContext _db;

    public RedistributionService(ApplicationDbContext db) => _db = db;

    public async Task<List<RedistributionRecommendation>> GenerateRecommendationsAsync(CancellationToken ct = default)
    {
        var items = await _db.InventoryItems
            .Include(i => i.Medicine).Include(i => i.Facility)
            .ToListAsync(ct);

        var created = new List<RedistributionRecommendation>();

        foreach (var group in items.GroupBy(i => i.MedicineId))
        {
            var surplus = group.Where(i => i.QuantityOnHand > i.MaxStockLevel).ToList();
            var shortage = group.Where(i => i.QuantityOnHand <= i.ReorderLevel).ToList();

            if (surplus.Count == 0 || shortage.Count == 0) continue;

            foreach (var need in shortage)
            {
                int needed = need.ReorderLevel - need.QuantityOnHand;
                if (needed <= 0) continue;

                foreach (var src in surplus.OrderByDescending(s => s.QuantityOnHand - s.MaxStockLevel))
                {
                    int available = src.QuantityOnHand - src.MaxStockLevel;
                    if (available <= 0) continue;

                    int qty = Math.Min(available, needed);
                    if (qty <= 0) continue;

                    bool exists = await _db.RedistributionRecommendations.AnyAsync(r =>
                        r.FromFacilityId == src.FacilityId &&
                        r.ToFacilityId == need.FacilityId &&
                        r.MedicineId == group.Key &&
                        r.Status == RedistributionStatus.Pending, ct);

                    if (!exists)
                    {
                        var rec = new RedistributionRecommendation
                        {
                            FromFacilityId = src.FacilityId,
                            ToFacilityId = need.FacilityId,
                            MedicineId = group.Key,
                            Quantity = qty,
                            Reason = $"{need.Facility.Name} below reorder level; {src.Facility.Name} has surplus."
                        };
                        _db.RedistributionRecommendations.Add(rec);
                        created.Add(rec);
                    }

                    needed -= qty;
                    if (needed <= 0) break;
                }
            }
        }

        if (created.Count > 0) await _db.SaveChangesAsync(ct);
        return created;
    }

    public async Task<List<RedistributionRecommendation>> GetRecommendationsAsync(int? facilityId = null)
    {
        var q = _db.RedistributionRecommendations
            .Include(r => r.Medicine)
            .Include(r => r.FromFacility)
            .Include(r => r.ToFacility)
            .AsQueryable();

        if (facilityId.HasValue)
            q = q.Where(r => r.FromFacilityId == facilityId || r.ToFacilityId == facilityId);

        return await q.OrderByDescending(r => r.CreatedAt).ToListAsync();
    }

    public async Task<bool> UpdateStatusAsync(int id, RedistributionStatus status, string userId)
    {
        var rec = await _db.RedistributionRecommendations.FirstOrDefaultAsync(r => r.Id == id);
        if (rec is null) return false;

        var allowedTransition = rec.Status switch
        {
            RedistributionStatus.Pending => status is RedistributionStatus.Accepted or RedistributionStatus.Rejected or RedistributionStatus.Cancelled,
            RedistributionStatus.Accepted => status is RedistributionStatus.Completed or RedistributionStatus.Cancelled,
            _ => false
        };
        if (!allowedTransition) return false;

        InventoryItem? fromItem = null;
        InventoryItem? toItem = null;
        if (status == RedistributionStatus.Completed)
        {
            fromItem = await _db.InventoryItems.FirstOrDefaultAsync(i =>
                i.FacilityId == rec.FromFacilityId && i.MedicineId == rec.MedicineId);
            toItem = await _db.InventoryItems.FirstOrDefaultAsync(i =>
                i.FacilityId == rec.ToFacilityId && i.MedicineId == rec.MedicineId);

            if (rec.Quantity <= 0 || fromItem is null || toItem is null || fromItem.QuantityOnHand < rec.Quantity)
                return false;
        }

        rec.Status = status;
        rec.ResolvedAt = DateTime.UtcNow;
        rec.ResolvedByUserId = userId;

        if (status == RedistributionStatus.Completed)
        {
            var now = DateTime.UtcNow;
            fromItem!.QuantityOnHand -= rec.Quantity;
            fromItem.LastUpdated = now;
            toItem!.QuantityOnHand += rec.Quantity;
            toItem.LastUpdated = now;
            _db.StockTransactions.AddRange(
                new StockTransaction
                {
                    InventoryItemId = fromItem.Id,
                    Type = TransactionType.Redistributed,
                    QuantityChange = -rec.Quantity,
                    Reason = $"Redistributed to facility #{rec.ToFacilityId} (rec #{rec.Id})",
                    UserId = userId,
                    Timestamp = now
                },
                new StockTransaction
                {
                    InventoryItemId = toItem.Id,
                    Type = TransactionType.Redistributed,
                    QuantityChange = rec.Quantity,
                    Reason = $"Received from facility #{rec.FromFacilityId} (rec #{rec.Id})",
                    UserId = userId,
                    Timestamp = now
                });
        }

        await _db.SaveChangesAsync();
        return true;
    }
}

