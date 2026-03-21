using DocTracking.Client.Models;
using DocTracking.Data;
using Microsoft.AspNetCore.Authorization;
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

            var hasCreatedDocuments = await _context.Documents.AnyAsync(d => d.CreatorId == id);
            if (hasCreatedDocuments)
            {
                var documentsToUpdate = await _context.Documents
                    .Where(d => d.CreatorId == id)
                    .ToListAsync();
                foreach (var doc in documentsToUpdate)
                    doc.CreatorId = null;
            }

            var userLogsToUpdate = await _context.DocumentLogs
                .Where(dl => dl.AppUserId == id)
                .ToListAsync();
            foreach (var log in userLogsToUpdate)
            {
                if (string.IsNullOrEmpty(log.UserName))
                    log.UserName = user.Name;
                log.AppUserId = null;
            }

            var userNotifications = await _context.AppNotifications
                .Where(n => n.AppUserId == id)
                .ToListAsync();
            if (userNotifications.Any())
                _context.AppNotifications.RemoveRange(userNotifications);

            _context.AppUsers.Remove(user);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut("bulk-reassign")]
        public async Task<IActionResult> BulkReassign([FromBody] BulkReassignRequest request)
        {
            var users = await _context.AppUsers
                .Where(u => u.UnitId == request.FromUnitId)
                .ToListAsync();

            if (!users.Any()) return Ok(new { moved = 0 });

            var targetUnit = await _context.Units.FindAsync(request.ToUnitId);
            if (targetUnit == null) return BadRequest("Target unit not found.");

            foreach (var user in users)
            {
                user.UnitId = request.ToUnitId;
                user.OfficeId = targetUnit.OfficeId;
            }

            await _context.SaveChangesAsync();
            return Ok(new { moved = users.Count });
        }

        [HttpPut("selective-reassign")]
        public async Task<IActionResult> SelectiveReassign([FromBody] SelectiveReassignRequest request)
        {
            if (request.UserIds == null || !request.UserIds.Any())
                return BadRequest("No users selected.");

            var targetUnit = await _context.Units.FindAsync(request.ToUnitId);
            if (targetUnit == null) return BadRequest("Target unit not found.");

            var users = await _context.AppUsers
                .Where(u => request.UserIds.Contains(u.Id))
                .ToListAsync();

            foreach (var user in users)
            {
                user.UnitId = request.ToUnitId;
                user.OfficeId = targetUnit.OfficeId;
            }

            await _context.SaveChangesAsync();
            return Ok(new { moved = users.Count });
        }
    }

    public class BulkReassignRequest
    {
        public int FromUnitId { get; set; }
        public int ToUnitId { get; set; }
    }

    public class SelectiveReassignRequest
    {
        public List<int> UserIds { get; set; } = new();
        public int ToUnitId { get; set; }
    }
}
