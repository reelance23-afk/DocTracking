using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using DocTracking.Client.Models;

namespace DocTracking.Data
{
    public class ApplicationDbContext : DbContext, IDataProtectionKeyContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Document> Documents { get; set; }
        public DbSet<Office> Offices { get; set; }
        public DbSet<DocumentLog> DocumentLogs { get; set; }
        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<AppNotification> AppNotifications { get; set; }
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Document>()
                .HasOne(d => d.CurrentOffice)
                .WithMany()
                .HasForeignKey(d => d.CurrentOfficeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Document>()
                .HasOne(m => m.NextOffice)
                .WithMany()
                .HasForeignKey(m => m.NextOfficeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Document>()
                .HasOne(d => d.Creator)
                .WithMany()
                .HasForeignKey(d => d.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DocumentLog>()
                .HasOne(d => d.AppUser)
                .WithMany()
                .HasForeignKey(d => d.AppUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AppUser>()
                .HasOne(u => u.Office)
                .WithMany()
                .HasForeignKey(u => u.OfficeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AppNotification>()
                .HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(n => n.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
