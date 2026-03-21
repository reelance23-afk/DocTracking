using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DocTracking.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=crossover.proxy.rlwy.net;Port=56546;Database=railway;Username=postgres;Password=ntQqnqwFwyYzhOGpdoIUmYfKkAUntPfn;")
                .Options;
            return new ApplicationDbContext(options);
        }
    }
}