
using MEDICSALLOCATION_2.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MEDICSALLOCATION_2.Data;

public static class SeedData
{
   
    public static async Task InitializeAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var r in new[] { "Admin", "PharmacyStaff", "Patient" })
            if (!await roleMgr.RoleExistsAsync(r))
                await roleMgr.CreateAsync(new IdentityRole(r));

        if (!await db.Facilities.AnyAsync())
        {
            db.Facilities.AddRange(
                new Facility { Name = "Princess Marina Hospital", Type = FacilityType.ReferralHospital, District = "Gaborone", ContactPhone = "+267 362 1400" },
                new Facility { Name = "Nyangabgwe Referral Hospital", Type = FacilityType.ReferralHospital, District = "Francistown" },
                new Facility { Name = "Athlone Hospital", Type = FacilityType.DistrictHospital, District = "Lobatse" },
                new Facility { Name = "Bamalete Lutheran Hospital", Type = FacilityType.DistrictHospital, District = "Ramotswa" },
                new Facility { Name = "Kanye SDA Hospital", Type = FacilityType.DistrictHospital, District = "Kanye" },
                new Facility { Name = "Molepolole Clinic", Type = FacilityType.Clinic, District = "Kweneng" },
                new Facility { Name = "Maun Clinic", Type = FacilityType.Clinic, District = "Ngamiland" },
                new Facility { Name = "Ghanzi Primary Hospital", Type = FacilityType.PrimaryHospital, District = "Ghanzi" });
            await db.SaveChangesAsync();
        }

        if (!await db.Medicines.AnyAsync())
        {
            db.Medicines.AddRange(
                new Medicine { Name = "Paracetamol 500mg", GenericName = "Paracetamol", Category = "Analgesic", Strength = "500mg", DosageForm = "Tablet", Unit = "tablets", IsEssential = true },
                new Medicine { Name = "Amoxicillin 500mg", GenericName = "Amoxicillin", Category = "Antibiotic", Strength = "500mg", DosageForm = "Capsule", Unit = "capsules", IsEssential = true },
                new Medicine { Name = "Metformin 500mg", GenericName = "Metformin", Category = "Antidiabetic", Strength = "500mg", DosageForm = "Tablet", Unit = "tablets", IsEssential = true },
                new Medicine { Name = "Amlodipine 5mg", GenericName = "Amlodipine", Category = "Antihypertensive", Strength = "5mg", DosageForm = "Tablet", Unit = "tablets", IsEssential = true },
                new Medicine { Name = "Salbutamol Inhaler", GenericName = "Salbutamol", Category = "Respiratory", DosageForm = "Inhaler", Unit = "inhalers", IsEssential = true },
                new Medicine { Name = "ORS Sachets", GenericName = "Oral Rehydration Salts", Category = "Rehydration", DosageForm = "Sachet", Unit = "sachets", IsEssential = true },
                new Medicine { Name = "Cotrimoxazole 960mg", GenericName = "Cotrimoxazole", Category = "Antibiotic", Strength = "960mg", DosageForm = "Tablet", Unit = "tablets", IsEssential = true },
                new Medicine { Name = "Insulin (Soluble)", GenericName = "Insulin", Category = "Antidiabetic", DosageForm = "Injection", Unit = "vials", IsEssential = true });
            await db.SaveChangesAsync();
        }

        if (!await db.InventoryItems.AnyAsync())
        {
            var rnd = new Random(42);
            var facilities = await db.Facilities.ToListAsync();
            var medicines = await db.Medicines.ToListAsync();

            foreach (var f in facilities)
                foreach (var m in medicines)
                    db.InventoryItems.Add(new InventoryItem
                    {
                        FacilityId = f.Id,
                        MedicineId = m.Id,
                        QuantityOnHand = rnd.Next(0, 500),
                        ReorderLevel = 20,
                        MaxStockLevel = 300,
                        BatchNumber = $"B{rnd.Next(1000, 9999)}",
                        ExpiryDate = DateTime.UtcNow.AddDays(rnd.Next(-30, 400)),
                        LastUpdated = DateTime.UtcNow
                    });

            await db.SaveChangesAsync();
        }

        var configuration = sp.GetRequiredService<IConfiguration>();
        var environment = sp.GetRequiredService<IHostEnvironment>();
        var adminEmail = configuration["InitialAdmin:Email"];
        var adminPassword = configuration["InitialAdmin:Password"];

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "Configure InitialAdmin:Email and InitialAdmin:Password in the hosting environment before starting the production site.");
            }

            return;
        }

        var admin = await userMgr.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "System Administrator",
                EmailConfirmed = true
            };
            var createAdmin = await userMgr.CreateAsync(admin, adminPassword);
            if (!createAdmin.Succeeded)
                throw new InvalidOperationException("Could not create the initial administrator account.");
        }

        if (!await userMgr.IsInRoleAsync(admin, "Admin"))
        {
            var addAdminRole = await userMgr.AddToRoleAsync(admin, "Admin");
            if (!addAdminRole.Succeeded)
                throw new InvalidOperationException("Could not assign the administrator role.");
        }

        // Existing accounts without a role receive the least-privileged patient role.
        foreach (var user in await userMgr.Users.ToListAsync())
        {
            if ((await userMgr.GetRolesAsync(user)).Count == 0)
                await userMgr.AddToRoleAsync(user, "Patient");
        }
    }
}

