namespace MEDICSALLOCATION_2.Models;

public enum TransactionType { Received, Dispensed, Adjusted, Redistributed, Expired, Returned }

public class StockTransaction
{
    public int Id { get; set; }

    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;

    public TransactionType Type { get; set; }
    public int QuantityChange { get; set; }
    public string? Reason { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? UserId { get; set; }
}
