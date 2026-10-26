using MEDICSALLOCATION_2.Models;

namespace MEDICSALLOCATION_2.Services;

public interface IRedistributionService
{
    Task<List<RedistributionRecommendation>> GenerateRecommendationsAsync(CancellationToken ct = default);
    Task<List<RedistributionRecommendation>> GetRecommendationsAsync(int? facilityId = null);
    Task<bool> UpdateStatusAsync(int id, RedistributionStatus status, string userId);
}
