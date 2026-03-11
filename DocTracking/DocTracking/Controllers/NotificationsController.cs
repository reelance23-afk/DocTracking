using DocTracking.Client.Models;
using DocTracking.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public NotificationsController(ApplicationDbContext context) => _context = context;

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
    }
}
