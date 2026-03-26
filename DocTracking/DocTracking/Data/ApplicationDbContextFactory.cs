using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DocTracking.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server =.\\SQLEXPRESS; Database = DocTrackingDB; Trusted_Connection = True; TrustServerCertificate = True; ")
                .Options;
            return new ApplicationDbContext(options);
        }
    }
}