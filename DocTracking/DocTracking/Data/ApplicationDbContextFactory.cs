using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DocTracking.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=shuttle.proxy.rlwy.net;Port=41015;Database=railway;Username=postgres;Password=KxuEFeImHxrsluZDXxNbpTnmBsGzrKzY;")
                .Options;
            return new ApplicationDbContext(options);
        }
    }
}
