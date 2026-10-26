namespace MEDICSALLOCATION_2.Models;

    
        public enum AlertType { LowStock, NearExpiry, Expired, Overstock}
        public enum AlertSeverity { Info, Warning, Critical }

        public class StockAlert
        {
            public int Id { get; set; }
            public int FacilityId { get; set; }
            public Facility Facility { get; set; } = null!;

            public int MedicineId { get; set; }
            public Medicine Medicine { get; set; } = null!;
        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }

    

