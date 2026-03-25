using DocTracking.Client.Models;
using DocTracking.Data;
using DocTracking.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DocTracking.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [EnableRateLimiting("api")]
    public class NotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly ILogger<NotificationsController> _logger;
        private const int MaxNotificationsPerUser = 50;

        public NotificationsController(ApplicationDbContext context, IHubContext<NotificationHub> hub, ILogger<NotificationsController> logger)
        {
            _context = context;
            _hub = hub;
            _logger = logger;
        }

        private async Task<AppUser?> GetCurrentUser() =>
            await _context.AppUsers.FirstOrDefaultAsync(u =>
                u.Email == (User.Identity!.Name ?? User.FindFirstValue(ClaimTypes.Email)));

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AppNotification>>> GetMyNotifications()
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();

            return await _context.AppNotifications
                .Where(n => n.AppUserId == user.Id)
                .OrderByDescending(n => n.Time)
                .Take(MaxNotificationsPerUser)
                .ToListAsync();
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();

            await _context.AppNotifications
                .Where(n => n.AppUserId == user.Id && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

            return Ok();
        }

        [HttpPost("broadcast")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Broadcast([FromBody] BroadcastRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Message is required.");

            var query = _context.AppUsers.AsQueryable();
            if (request.OfficeId.HasValue)
                query = query.Where(u => u.OfficeId == request.OfficeId ||
                    (u.Unit != null && u.Unit.OfficeId == request.OfficeId));

            var targets = await query.ToListAsync();
            var now = DateTime.UtcNow;

            _context.AppNotifications.AddRange(targets.Select(u => new AppNotification
            {
                AppUserId = u.Id,
                Message = request.Message,
                DocumentName = "",
                Time = now
            }));

            try
            {
                await _context.SaveChangesAsync();
                await PruneNotificationsAsync(targets.Select(u => u.Id).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Broadcast] SaveChanges failed");
                return StatusCode(500, "Failed to save notifications.");
            }

            foreach (var user in targets)
            {
                try
                {
                    await _hub.Clients.Group($"user-{user.Id}")
                        .SendAsync("ReceiveNotification", request.Message, "");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Broadcast] SignalR failed for user {UserId}", user.Id);
                }
            }

            return Ok(new { sent = targets.Count });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DismissNotification(int id)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();

            var notif = await _context.AppNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.AppUserId == user.Id);
            if (notif == null) return NotFound();

            _context.AppNotifications.Remove(notif);
            try { await _context.SaveChangesAsync(); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DismissNotification] Failed for notification {Id}", id);
                return StatusCode(500, "Failed to dismiss notification.");
            }
            return Ok();
        }

        [HttpPut("{id}/toggle-read")]
        public async Task<IActionResult> ToggleRead(int id)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();

            var notif = await _context.AppNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.AppUserId == user.Id);
            if (notif == null) return NotFound();

            notif.IsRead = !notif.IsRead;
            try { await _context.SaveChangesAsync(); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ToggleRead] Failed for notification {Id}", id);
                return StatusCode(500, "Failed to update notification.");
            }
            return Ok(new { isRead = notif.IsRead });
        }

        private async Task PruneNotificationsAsync(List<int> userIds)
        {
            foreach (var userId in userIds)
            {
                var keepIds = await _context.AppNotifications
                    .Where(n => n.AppUserId == userId)
                    .OrderByDescending(n => n.Time)
                    .Take(MaxNotificationsPerUser)
                    .Select(n => n.Id)
                    .ToListAsync();

                await _context.AppNotifications
                    .Where(n => n.AppUserId == userId && !keepIds.Contains(n.Id))
                    .ExecuteDeleteAsync();
            }
        }

        public class BroadcastRequest
        {
            public string Message { get; set; } = "";
            public int? OfficeId { get; set; }
        }
    }
}
