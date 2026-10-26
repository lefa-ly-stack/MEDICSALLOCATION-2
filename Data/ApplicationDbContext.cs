using MEDICSALLOCATION_2.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace MEDICSALLOCATION_2.Data;


    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Facility> Facilities => Set<Facility>();
        public DbSet<Medicine> Medicines => Set<Medicine>();
        public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
        public DbSet<StockAlert> StockAlerts => Set<StockAlert>();
        public DbSet<RedistributionRecommendation> RedistributionRecommendations => Set<RedistributionRecommendation>();
        public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();

        protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<InventoryItem>()
            .HasIndex(i => new { i.FacilityId, i.MedicineId, i.BatchNumber });

        b.Entity<RedistributionRecommendation>()
            .HasOne(r => r.FromFacility).WithMany()
            .HasForeignKey(r => r.FromFacilityId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<RedistributionRecommendation>()
            .HasOne(r => r.ToFacility).WithMany()
            .HasForeignKey(r => r.ToFacilityId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockAlert>()
           .HasOne(a => a.Facility).WithMany()
           .HasForeignKey(a => a.FacilityId).OnDelete(DeleteBehavior.Restrict);
    }


}
