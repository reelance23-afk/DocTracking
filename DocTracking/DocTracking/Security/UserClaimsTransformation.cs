using Microsoft.AspNetCore.Authentication;
using DocTracking.Data;
using DocTracking.Client.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace DocTracking.Security
{
    public class UserClaimsTransformation : IClaimsTransformation
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public UserClaimsTransformation(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity is not { IsAuthenticated: true }) return principal;

            var email = principal.Identity.Name;
            if (string.IsNullOrEmpty(email)) return principal;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var dbUser = await db.AppUsers
                .Include(u => u.Unit)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (dbUser == null)
            {
                dbUser = new AppUser { Email = email, Role = "User"};
                db.AppUsers.Add(dbUser);
                await db.SaveChangesAsync();
            }

            var newIdentity = new ClaimsIdentity();
            newIdentity.AddClaim(new Claim(ClaimTypes.Role, dbUser.Role ?? "User"));
            newIdentity.AddClaim(new Claim("roles", dbUser.Role ?? "User"));

            if (dbUser.UnitId.HasValue)
            {
                newIdentity.AddClaim(new Claim("OfficeId", dbUser.UnitId.Value.ToString()));
            }

            if (dbUser.Unit != null)
            {
                newIdentity.AddClaim(new Claim("OfficeId", dbUser.Unit.OfficeId.ToString()));
            }

            principal.AddIdentity(newIdentity);
            return principal;
        }
    }
}
