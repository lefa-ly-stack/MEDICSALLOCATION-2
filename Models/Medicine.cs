using System.ComponentModel.DataAnnotations;

namespace MEDICSALLOCATION_2.Models;

public class Medicine
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength (150)] public string? GenericName { get; set; }
    [StringLength(80)] public string? Category { get; set; }
    [StringLength(50)] public string? Strength { get; set; }
    [StringLength(50)] public string? DosageForm { get; set; }
    [StringLength(20)] public string? Unit { get; set; }
    public bool IsEssential { get; set; }

    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
}
