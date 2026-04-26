using DocTracking.Client.Models;
using DocTracking.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace DocTracking.Security
{
    public class UserClaimsTransformation : IClaimsTransformation
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMemoryCache _cache;

        public UserClaimsTransformation(IServiceScopeFactory scopeFactory, IMemoryCache cache)
        {
            _scopeFactory = scopeFactory;
            _cache = cache;
        }

        public static string CacheKey(string email) => $"user-claims-{email}";

        public void InvalidateUser(string email) => _cache.Remove(CacheKey(email));

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity is not ClaimsIdentity originalIdentity || !originalIdentity.IsAuthenticated)
                return principal;

            var email = originalIdentity.Name
                ?? principal.FindFirst(ClaimTypes.Email)?.Value
                ?? principal.FindFirst("preferred_username")?.Value;

            var name = principal.FindFirst("name")?.Value ?? email;

            var cacheKey = CacheKey(email!);
            if (!_cache.TryGetValue(cacheKey, out AppUser? dbUser))
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                try
                {
                    dbUser = await db.AppUsers
                        .Include(u => u.Unit)
                        .FirstOrDefaultAsync(u => u.Email == email);

                    if (dbUser == null)
                    {
                        var hasAdmin = await db.AppUsers.AnyAsync(u => u.Role == "Admin");
                        dbUser = new AppUser { Email = email, Name = name, Role = hasAdmin ? "User" : "Admin" };
                        db.AppUsers.Add(dbUser);
                        await db.SaveChangesAsync();
                    }
                    else if (dbUser.Name == null)
                    {
                        dbUser.Name = name;
                        await db.SaveChangesAsync();
                    }

                    _cache.Set(cacheKey, dbUser, TimeSpan.FromMinutes(5));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ClaimsTransform] DB error for {email}: {ex.Message}");
                    return principal;
                }
            }

            var clone = principal.Clone();
            var identity = clone.Identity as ClaimsIdentity;

            if (identity != null)
            {
                var oldClaims = identity.Claims.Where(c =>
                    c.Type.Contains("role", StringComparison.OrdinalIgnoreCase) ||
                    c.Type == "UnitId" || c.Type == "OfficeId").ToList();
                foreach (var c in oldClaims) identity.RemoveClaim(c);

                identity.AddClaim(new Claim(ClaimTypes.Role, dbUser!.Role ?? "User"));
                identity.AddClaim(new Claim("roles", dbUser.Role ?? "User"));
                identity.AddClaim(new Claim("IsOfficeHead", dbUser.IsOfficeHead.ToString()));

                if (dbUser.UnitId.HasValue)
                {
                    identity.AddClaim(new Claim("UnitId", dbUser.UnitId.Value.ToString()));
                    identity.AddClaim(new Claim("OfficeId", dbUser.Unit!.OfficeId.ToString()));
                }
                else if (dbUser.OfficeId.HasValue)
                {
                    identity.AddClaim(new Claim("OfficeId", dbUser.OfficeId.Value.ToString()));
                }
            }

            return clone;
        }
    }
}
