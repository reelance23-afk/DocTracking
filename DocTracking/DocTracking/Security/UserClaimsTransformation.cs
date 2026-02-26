using Microsoft.AspNetCore.Authentication;
using DocTracking.Data;
using DocTracking.Client.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Extensions;
using System.Security.Cryptography.Pkcs;
using Microsoft.Graph;

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
            if (principal.Identity is not ClaimsIdentity originalIdentity || !originalIdentity.IsAuthenticated) return principal;

            var email = originalIdentity.Name
                ?? principal.FindFirst(ClaimTypes.Email)?.Value
                ?? principal.FindFirst("preferred_username")?.Value;

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

            var clone = principal.Clone();
            var identity = clone.Identity as ClaimsIdentity;

            if(identity != null)
            {
                var oldRoles = identity.Claims.Where(c =>
                c.Type.Contains("role", StringComparison.OrdinalIgnoreCase) ||
                c.Type == "UnitId" ||
                c.Type == "OfficeId")
                    .ToList();
                foreach(var oldRole in oldRoles)
                {
                    identity.RemoveClaim(oldRole);
                }

                identity.AddClaim(new Claim(ClaimTypes.Role, dbUser.Role ?? "User"));
                identity.AddClaim(new Claim("roles", dbUser.Role ?? "User"));

                if (dbUser.UnitId.HasValue)
                {
                    identity.AddClaim(new Claim("UnitId", dbUser.UnitId.Value.ToString()));
                    identity.AddClaim(new Claim("OfficeId", dbUser.Unit!.OfficeId.ToString()));
                }
            }


            return clone;
        }
    }
}
