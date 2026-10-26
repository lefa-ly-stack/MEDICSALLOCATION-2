namespace MEDICSALLOCATION_2.Models;

    public class InventoryItem
    {
        public int Id { get; set; }

        public int FacilityId { get; set; }
        public Facility Facility { get; set; } = null!;

        public int MedicineId { get; set; }
        public Medicine Medicine { get; set; } = null!;

        public int QuantityOnHand { get; set; }
        public int ReorderLevel { get; set; } = 20;
        public int MaxStockLevel { get; set; } = 300;

        public string? BatchNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        public string? UpdatedByUserId { get; set; }

        public ICollection<StockTransaction> Transactions { get; set; } = new List<StockTransaction>();
    }

