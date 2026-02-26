using DocTracking.Data;
using DocTracking.Client.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocTracking.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DocumentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<Document>> CreateDocument(Document doc)
        {
            doc.CreatedAt = DateTime.UtcNow;
            doc.Status = "On Going";

            _context.Documents.Add(doc);
            await _context.SaveChangesAsync();

            var log = new DocumentLog
            {
                DocumentId = doc.Id,
                Action = "Created",
                TimeStamp = DateTime.UtcNow,
                OfficeId = doc.NextOfficeId,
                UnitId = doc.NextUnitId,
                AppUserId = doc.CreatorId
            };

            _context.DocumentLogs.Add(log);
            await _context.SaveChangesAsync();

            return Ok(doc);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Document>>> GetAllDocument()
        {
            return await _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.NextOffice)
                .Include(d => d.CurrentOffice)
                .Include(d => d.NextUnit)
                .Include(d => d.CurrentUnit)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        [HttpGet("user/{email}")]
        public async Task<ActionResult<IEnumerable<Document>>> GetUserDocument(string email)
        {
            return await _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.NextOffice)
                .Include(d => d.NextUnit)
                .Where(d => d.Creator != null && d.Creator.Email == email)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        [HttpGet("incoming/{officeId}")]
        public async Task<ActionResult<IEnumerable<Document>>> GetIncoming(int officeId)
        {
            return await _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.NextOffice)
                .Include(d => d.NextUnit)
                .Where(d => d.NextOfficeId == officeId && d.Status == "On Going")
                .ToListAsync();
        }

        [HttpPut("{id}/receive")]
        public async Task<IActionResult> ReceiveDocument(int id)
        {
            var doc = await _context.Documents.FindAsync(id);
            if (doc == null) return NotFound();

            doc.Status = "Received";
            doc.LastActionDate = DateTime.UtcNow;

            doc.CurrentOfficeId = doc.NextOfficeId;
            doc.CurrentUnitId = doc.NextUnitId;


            doc.NextOfficeId = null;
            doc.NextUnitId = null;

            _context.DocumentLogs.Add(new DocumentLog
            {
                DocumentId = doc.Id,
                Action = "Received",
                OfficeId = doc.CurrentOfficeId,
                UnitId = doc.CurrentUnitId,
                TimeStamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut("{id}/forward")]
        public async Task<IActionResult> ForwardDocument(int id, [FromBody] ForwardRequest request)
        {
            var doc = await _context.Documents.FindAsync(id);
            if (doc == null) return NotFound();

            doc.Status = "On Going";
            doc.LastActionDate = DateTime.UtcNow;

            doc.NextOfficeId = request.NextOfficeId;
            doc.NextUnitId = request.NextUnitId;

            doc.CurrentOfficeId = null;
            doc.CurrentUnitId = null;

            _context.DocumentLogs.Add(new DocumentLog
            {
                DocumentId = doc.Id,
                Action = "Forwarded",
                OfficeId = request.NextOfficeId,
                UnitId = request.NextUnitId,
                TimeStamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok();
        }

        public class ForwardRequest
        {
            public int NextOfficeId { get; set; }
            public int? NextUnitId { get; set; }
        }
    }
}
