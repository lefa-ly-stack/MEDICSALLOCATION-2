using MEDICSALLOCATION_2.Models;

namespace MEDICSALLOCATION_2.Services;

public interface IAlertService
{
    Task<int> GenerateAlertsAsync(CancellationToken ct = default);
    Task<List<StockAlert>> GetActiveAlertsAsync(int? facilityId = null);
    Task<bool> ResolveAlertAsync(int alertId, int? facilityId = null);
}


