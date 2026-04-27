using DocTracking.Client.Models;
using DocTracking.Data;
using DocTracking.Hubs;
using DocTracking.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;


namespace DocTracking.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("api")]
    public class AppUsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserClaimsTransformation _claimsTransform;
        private readonly ILogger<AppUsersController> _logger;
        private readonly IHubContext<NotificationHub> _hub;

        public AppUsersController(ApplicationDbContext context, UserClaimsTransformation claimsTransform,
            IHubContext<NotificationHub> hub, ILogger<AppUsersController> logger)
        {
           _context = context;
            _claimsTransform = claimsTransform;
            _hub = hub;
            _logger = logger;
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] AppUser updatedUser)
        {
            var user = await _context.AppUsers.FindAsync(id);
            if (user == null) return NotFound();

            if (user.Role == "Admin" && updatedUser.Role != "Admin")
            {
                var adminCount = await _context.AppUsers.CountAsync(u => u.Role == "Admin");
                if (adminCount <= 1)
                    return BadRequest("Cannot change the role of the last Admin in the system.");
            }

            user.Role = updatedUser.Role;
            user.UnitId = updatedUser.UnitId;
            user.OfficeId = updatedUser.OfficeId;
            user.IsOfficeHead = updatedUser.IsOfficeHead;

            try
            {
                await _context.SaveChangesAsync();
                if (user.Email != null) _claimsTransform.InvalidateUser(user.Email);
                Console.WriteLine($"[UpdateUser] Sending RoleChanged to user-{user.Id}");
                await _hub.Clients.Group($"user-{user.Id}").SendAsync("RoleChanged");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UpdateUser] Failed for user {Id}", id);
                return StatusCode(500, "Failed to update user.");
            }
            return Ok();
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.AppUsers.FindAsync(id);
            if (user == null) return NotFound();

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.Documents
                    .Where(d => d.CreatorId == id)
                    .ExecuteUpdateAsync(d => d.SetProperty(x => x.CreatorId, (int?)null));

                await _context.DocumentLogs
                    .Where(dl => dl.AppUserId == id && (dl.UserName == null || dl.UserName == ""))
                    .ExecuteUpdateAsync(dl => dl.SetProperty(x => x.UserName, user.Name));

                await _context.DocumentLogs
                    .Where(dl => dl.AppUserId == id)
                    .ExecuteUpdateAsync(dl => dl.SetProperty(x => x.AppUserId, (int?)null));

                await _context.AppNotifications
                    .Where(n => n.AppUserId == id)
                    .ExecuteDeleteAsync();

                _context.AppUsers.Remove(user);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "[DeleteUser] Failed for user {Id}", id);
                return StatusCode(500, "Failed to delete user.");
            }

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
                user.Role = user.Role == "Admin" ? "Admin" : "Office";
            }

            try
            {
                await _context.SaveChangesAsync();
                foreach (var user in users)
                    if (user.Email != null) _claimsTransform.InvalidateUser(user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BulkReassign] Failed");
                return StatusCode(500, "Failed to reassign users.");
            }
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
                user.Role = user.Role == "Admin" ? "Admin" : "Office";
            }

            try
            {
                await _context.SaveChangesAsync();
                foreach (var user in users)
                    if (user.Email != null) _claimsTransform.InvalidateUser(user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SelectiveReassign] Failed");
                return StatusCode(500, "Failed to reassign users.");
            }
            return Ok(new { moved = users.Count });
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<AppUser>>> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? search = null,
            [FromQuery] int? officeId = null,
            [FromQuery] int? unitId = null,
            [FromQuery] bool filterByOffice = false)
        {
            var query = _context.AppUsers
                .Include(u => u.Unit)
                .ThenInclude(unit => unit.Office)
                .Include(u => u.Office)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(u =>
                    (u.Name != null && u.Name.Contains(search)) ||
                    (u.Role != null && u.Role.Contains(search)));

            if (filterByOffice && officeId.HasValue)
                query = query.Where(u => u.OfficeId == officeId || u.Unit.OfficeId == officeId);

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(u => unitId.HasValue && u.UnitId == unitId ? 0 :
                              officeId.HasValue && (u.OfficeId == officeId || (u.Unit != null && u.Unit.OfficeId == officeId)) ? 1 : 2)
                .ThenBy(u => u.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new PagedResult<AppUser> { Items = items, TotalCount = total });
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
