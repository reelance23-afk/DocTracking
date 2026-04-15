using DocTracking.Client.Models;
using DocTracking.Data;
using DocTracking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DocTracking.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [EnableRateLimiting("api")]
    public class DocumentLogsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly DocumentQueryService _docService;
        private readonly ILogger<DocumentLogsController> _logger;

        public DocumentLogsController(ApplicationDbContext context, DocumentQueryService docService, ILogger<DocumentLogsController> logger)
        {
            _context = context;
            _docService = docService;
            _logger = logger;
        }

        [HttpGet("{documentId}")]
        public async Task<ActionResult<IEnumerable<DocumentLog>>> GetDocumentLogs(int documentId)
        {
            try
            {
                return await _context.DocumentLogs
                    .Include(m => m.Document)
                    .Include(m => m.Office)
                    .Include(m => m.Unit)
                    .Include(m => m.AppUser)
                    .ThenInclude(m => m.Unit)
                    .ThenInclude(m => m.Office)
                    .Where(m => m.DocumentId == documentId)
                    .OrderByDescending(m => m.TimeStamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetDocumentLogs] Failed for documentId {DocumentId}", documentId);
                return StatusCode(500, "Failed to load document logs.");
            }
        }

        [HttpGet("audit")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<PagedResult<DocumentLog>>> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? action = null,
        [FromQuery] string? date = null,
        [FromQuery] string? sender = null,
        [FromQuery] string? office = null)
        {
            var (items, total) = await _docService.GetAuditLogsAsync(page, pageSize, search, action, date, sender, office);
            return Ok(new PagedResult<DocumentLog> { Items = items, TotalCount = total });
        }

        [HttpGet("export-csv")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportAuditLogCsv(
            [FromQuery] string? search = null,
            [FromQuery] string? action = null,
            [FromQuery] string? date = null,
            [FromQuery] string? sender = null,
            [FromQuery] string? office = null)
        {
            try
            {
                Response.ContentType = "text/csv";
                Response.Headers.Append("Content-Disposition", $"attachment; filename=auditlog_{DateTime.Now:yyyyMMdd}.csv");

                var sb = new StringBuilder();
                sb.AppendLine("Timestamp,Document,Reference,Action,By,Office,Unit,Comment");

                await foreach (var log in _docService.StreamAllAuditLogsAsync(search, action, date, sender, office))
                {
                    sb.AppendLine($"\"{log.TimeStamp.ToLocalTime():yyyy-MM-dd hh:mm tt}\"," +
                                  $"\"{log.Document?.Name}\"," +
                                  $"\"{log.Document?.ReferenceNumber}\"," +
                                  $"\"{log.Action}\"," +
                                  $"\"{log.AppUser?.Name}\"," +
                                  $"\"{log.Office?.Name ?? log.OfficeName}\"," +
                                  $"\"{log.Unit?.Name ?? log.UnitName}\"," +
                                  $"\"{log.Comment}\"");
                }

                return Content(sb.ToString(), "text/csv", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExportAuditLogCsv] Failed");
                return StatusCode(500, "Failed to export audit log.");
            }
        }

    }
}
