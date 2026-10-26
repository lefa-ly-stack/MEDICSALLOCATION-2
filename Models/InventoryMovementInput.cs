using System.ComponentModel.DataAnnotations;

namespace MEDICSALLOCATION_2.Models;

public class InventoryMovementInput
{
    [Required]
    public TransactionType Type { get; set; } = TransactionType.Received;

    [Range(1, int.MaxValue, ErrorMessage = "Enter a quantity greater than zero.")]
    public int Quantity { get; set; }

    [StringLength(250)]
    public string? Reason { get; set; }
}
