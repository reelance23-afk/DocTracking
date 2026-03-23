using DocTracking.Client.Models;
using DocTracking.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DocTracking.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [EnableRateLimiting("api")]
    public class DocumentLogsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DocumentLogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("{documentId}")]
        public async Task<ActionResult<IEnumerable<DocumentLog>>> GetDocumentLogs(int documentId)
        {
            return await _context.DocumentLogs
                .Include(m => m.Document)
                .Include(m => m.Office)
                .Include(m => m.Unit)
                .Include(m => m.AppUser)
                .ThenInclude(m => m.Unit)
                .ThenInclude(m => m.Office)
                .Where(m => m.DocumentId == documentId )
                .OrderByDescending(m => m.TimeStamp)
                .ToListAsync();
        }

        [HttpGet("audit")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<PagedResult<DocumentLog>>> GetAuditLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? search = null,
            [FromQuery] string? action = null,
            [FromQuery] string? date = null)
        {
            var query = _context.DocumentLogs
                .Include(m => m.Document)
                .Include(m => m.Office)
                .Include(m => m.Unit)
                .Include(m => m.AppUser)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(m =>
                    (m.Document != null && m.Document.Name != null && m.Document.Name.Contains(search)) ||
                    (m.Document != null && m.Document.ReferenceNumber != null && m.Document.ReferenceNumber.Contains(search)) ||
                    (m.AppUser != null && m.AppUser.Name != null && m.AppUser.Name.Contains(search)) ||
                    (m.Action != null && m.Action.Contains(search)));

            if (!string.IsNullOrEmpty(action))
                query = query.Where(m => m.Action == action);

            if (DateTime.TryParse(date, out var filterDate))
                query = query.Where(m => m.TimeStamp.Date == filterDate.Date);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(m => m.TimeStamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new PagedResult<DocumentLog> { Items = items, TotalCount = total });
        }


    }
}
