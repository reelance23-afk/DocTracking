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
                OfficeId = doc.NextOfficeId ?? 0
            };

            _context.DocumentLogs.Add(log);
            await _context.SaveChangesAsync();

            return Ok(doc);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Document>>> GetAllDocument()
        {
            return await _context.Documents
                .Include(d => d.NextOffice)
                .Include(d => d.CurrentOffice)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        [HttpGet("user/{email}")]
        public async Task<ActionResult<IEnumerable<Document>>> GetUserDocument(string email)
        {
            return await _context.Documents
                .Include(d => d.NextOffice)
                .Where(d => d.OriginalUserEmail == email)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        [HttpGet("incoming/{officeId}")]
        public async Task<ActionResult<IEnumerable<Document>>> GetIncoming(int officeId)
        {
            return await _context.Documents
                .Where(d => d.NextOfficeId == officeId && d.Status == "On Going")
                .Include(d => d.NextOffice)
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
            doc.NextOfficeId = null;

            _context.DocumentLogs.Add(new DocumentLog
            {
                DocumentId = doc.Id,
                Action = "Received",
                OfficeId = doc.CurrentOfficeId ?? 0,
                TimeStamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut("{id}/forward")]
        public async Task<IActionResult> ForwardDocument(int id, [FromBody] int nextOfficeId)
        {
            var doc = await _context.Documents.FindAsync(id);
            if (doc == null) return NotFound();

            doc.Status = "On Going";
            doc.LastActionDate = DateTime.UtcNow;

            doc.NextOfficeId = nextOfficeId;

            _context.DocumentLogs.Add(new DocumentLog
            {
                DocumentId = doc.Id,
                Action = "Forwarded",
                OfficeId = nextOfficeId,
                TimeStamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
