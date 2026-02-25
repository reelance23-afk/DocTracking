using DocTracking.Data;
using DocTracking.Client.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocTracking.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
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
                .Include(m => m.Office)
                .Include(m => m.Unit)
                .Include(m => m.AppUser)
                .Where(m => m.DocumentId == documentId )
                .OrderByDescending(m => m.TimeStamp)
                .ToListAsync();
        }

    }
}
