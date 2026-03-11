using DocTracking.Client.Models;
using DocTracking.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocTracking.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AppUsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AppUsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AppUser>>> GetUsers()
        {
            return await _context.AppUsers
                .Include(u => u.Unit)
                .ThenInclude(unit => unit.Office)
                .Include(u => u.Office)
                .OrderBy(u => u.Name)
                .ToListAsync();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] AppUser updatedUser)
        {
            var user = await _context.AppUsers.FindAsync(id);
            if (user == null) return NotFound();

            user.Role = updatedUser.Role;
            user.UnitId = updatedUser.UnitId;
            user.OfficeId = updatedUser.OfficeId;
            user.IsOfficeHead = updatedUser.IsOfficeHead;

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.AppUsers.FindAsync(id);
            if (user == null) return NotFound();

            _context.AppUsers.Remove(user);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
