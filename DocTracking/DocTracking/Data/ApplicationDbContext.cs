using Microsoft.EntityFrameworkCore;
using DocTracking.Client.Models;

namespace DocTracking.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Office> Offices { get; set; }
        public DbSet<DocumentLog> DocumentLogs { get; set; }

        public DbSet<AppUser> AppUsers { get; set; }

        public DbSet<Unit> Units { get; set; }

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
        }
    }
}
