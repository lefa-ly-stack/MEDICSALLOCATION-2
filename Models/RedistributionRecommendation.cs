namespace MEDICSALLOCATION_2.Models;

    public enum RedistributionStatus { Pending, Accepted, Rejected ,Completed, Cancelled}
    public class RedistributionRecommendation
    {
        public int Id { get; set; }

        public int FromFacilityId { get; set; }
        public Facility FromFacility { get; set; } = null!;
        public int ToFacilityId { get; set; }
        public Facility ToFacility { get; set; } = null!;

        public int MedicineId { get; set; }
        public Medicine Medicine { get; set; } = null!;

        public int Quantity { get; set; }
        public RedistributionStatus Status { get; set; } = RedistributionStatus.Pending;
        public string Reason { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedByUserId { get; set; }
    }

