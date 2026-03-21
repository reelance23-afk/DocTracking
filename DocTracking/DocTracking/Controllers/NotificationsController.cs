using DocTracking.Client.Models;
using DocTracking.Data;
using DocTracking.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DocTracking.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hub;

        public NotificationsController(ApplicationDbContext context, IHubContext<NotificationHub> hub)
        {
            _context = context;
            _hub = hub;
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
                .Take(50)
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
            await _context.SaveChangesAsync();

            foreach (var user in targets)
                await _hub.Clients.Group($"user-{user.Id}")
                    .SendAsync("ReceiveNotification", request.Message, "");

            return Ok(new { sent = targets.Count });
        }
    }

    public class BroadcastRequest
    {
        public string Message { get; set; } = "";
        public int? OfficeId { get; set; }
    }
}
