using Microsoft.AspNetCore.Identity;

namespace MEDICSALLOCATION_2.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public int? FacilityId { get; set; }
    public Facility? Facility { get; set; }
}


