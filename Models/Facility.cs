using System.ComponentModel.DataAnnotations;

namespace MEDICSALLOCATION_2.Models;

public enum FacilityType
{
    Clinic, PrimaryHospital, DistrictHospital, ReferralHospital, PrivatePharmacy, PrivateHospital, Wholesaler

}
public class Facility
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public FacilityType Type { get; set; }

    [Required, StringLength(80)]
    public string District { get; set; } = string.Empty;

    [StringLength(250)] public string? Address { get; set; }
    [Phone, StringLength(30)] public string? ContactPhone { get; set; }
    [EmailAddress, StringLength(120)] public string? ContactEmail { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
}

