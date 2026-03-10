using DocTracking.Data;
using DocTracking.Client.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.Graph;
using Microsoft.AspNetCore.Authentication;

namespace DocTracking.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DocumentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public DocumentsController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpPost]
        public async Task<ActionResult<Document>> CreateDocument([FromBody] Document doc)
        {

            var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
            var appuser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Email == email);

            doc.CreatedAt = DateTime.UtcNow;
            doc.Status = "In Motion";
            doc.CreatorId = appuser?.Id;

            string randomNum = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
            doc.ReferenceNumber = $"DOC-{DateTime.UtcNow:yyyyMMdd}-{randomNum}";
            doc.LastActionDate = DateTime.UtcNow;

            _context.Documents.Add(doc);
            await _context.SaveChangesAsync();

            var log = new DocumentLog
            {
                DocumentId = doc.Id,
                Action = "Created",
                TimeStamp = DateTime.UtcNow,
                OfficeId = doc.NextOfficeId,
                UnitId = doc.NextUnitId,
                AppUserId = appuser?.Id
            };

            _context.DocumentLogs.Add(log);
            await _context.SaveChangesAsync();

            return Ok(doc);
        }

     
        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null) return BadRequest("No File Uploaded");

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            if (!System.IO.Directory.Exists(uploadsFolder)) System.IO.Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Ok(new { FilePath = $"/uploads/{uniqueFileName}" });

        }

        [HttpDelete("upload")]
        public IActionResult DeleteUpload([FromQuery] string path)
        {
            var fullPath = Path.Combine(_env.WebRootPath, path.TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
            return Ok();
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

        /*
        [HttpGet("my-photo")]
        public async Task<IActionResult> GetMyPhoto()
        {
            var token = await HttpContext.GetTokenAsync("access_token");
            if (token == null) return NotFound();

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("https://graph.microsoft.com/v1.0/me/photo/$value");
            if (!response.IsSuccessStatusCode) return NotFound($"Graph returned {response.StatusCode}");

            var bytes = await response.Content.ReadAsByteArrayAsync();
            return File(bytes, "image/jpeg");
        }  */

        [HttpGet("my-profile")]
        public async Task<ActionResult<AppUser>> GetMyProfile()
        {
            var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);

            var appUser = await _context.AppUsers
                .Include(d => d.Unit)
                .Include(d => d.Office)
                .FirstOrDefaultAsync(d => d.Email == email);

            return appUser == null ? NotFound() : Ok(appUser);

        }

        [HttpGet("user/{email}")]
        public async Task<ActionResult<IEnumerable<Document>>> GetUserDocument(string email)
        {
            return await _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.NextOffice)
                .Include(d => d.NextUnit)
                .Include(d => d.CurrentOffice)
                .Include(d => d.CurrentUnit)
                .Where(d => d.Creator != null && d.Creator.Email == email)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        [HttpGet("incoming/{officeId}")]
        public async Task<ActionResult<IEnumerable<Document>>> GetIncoming(int officeId, [FromQuery] int? unitId = null )
        {
            var query = _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.NextOffice)
                .Include(d => d.NextUnit)
                .Where(d => d.NextOfficeId == officeId && d.Status == "In Motion");

            if (unitId.HasValue)
            {
                query = query.Where(d => d.NextUnitId == unitId || d.NextUnitId == null);
            }
            else
            {
                query = query.Where(d => d.NextUnitId == null);
            }

            return await query.ToListAsync();
        }

        [HttpPut("{id}/receive")]
        public async Task<IActionResult> ReceiveDocument(int id)
        {
            var doc = await _context.Documents.FindAsync(id);
            if (doc == null) return NotFound();
            if (doc.Status != "In Motion") return Conflict("Document has already been Received");

            var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
            var appUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Email == email);

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
                AppUserId = appUser?.Id,
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

            var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
            var appUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Email == email);

            doc.Status = "In Motion";
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
                AppUserId = appUser?.Id,
                TimeStamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut("{id}/finish")]
        public async Task<IActionResult> FinishDocument(int id)
        {
            var doc = await _context.Documents.FindAsync(id);
            if (doc == null) return NotFound();

            var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
            var appUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Email == email);

            doc.Status = "Completed";
            doc.LastActionDate = DateTime.UtcNow;

            var finishedAtOffice = doc.CurrentOfficeId;
            var finishedAtUnit = doc.CurrentUnitId;


            doc.CurrentOffice = null;
            doc.CurrentUnit = null;
            doc.CurrentOfficeId = null;
            doc.CurrentUnitId = null;

            doc.NextOffice = null;
            doc.NextUnit = null;
            doc.NextOfficeId = null;
            doc.NextUnitId = null;

            _context.DocumentLogs.Add(new DocumentLog
            {
                DocumentId = doc.Id,
                Action = "Completed",
                OfficeId = finishedAtOffice,
                UnitId = finishedAtUnit,
                AppUserId = appUser?.Id,
                TimeStamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("outgoing/user/{email}")]
        public async Task<ActionResult<IEnumerable<Document>>> GetOutGoing(string email)
        {
            var appUser = await _context.AppUsers.Include(u => u.Unit).FirstOrDefaultAsync(u => u.Email == email);
            if (appUser == null) return NotFound();

            int? myOfficeId = appUser.Unit?.OfficeId ?? appUser.OfficeId;
            int? myUnitId = appUser.UnitId;

            var query = _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.NextOffice)
                .Include(d => d.NextUnit)
                .Where(d => d.Status == "In Motion");

            if (myUnitId.HasValue)
            {
                query = query.Where(d => _context.DocumentLogs.Any(log =>
                log.DocumentId == d.Id &&
                log.Action == "Forwarded" &&
                log.AppUser != null &&
                log.AppUser.UnitId == myUnitId));
            }
            else if (myOfficeId.HasValue)
            {
                query = query.Where(d => _context.DocumentLogs.Any(log =>
                log.DocumentId == d.Id &&
                log.Action == "Forwarded" &&
                log.AppUser != null &&
                (
                    (log.AppUser.Unit != null && log.AppUser.Unit.OfficeId == myOfficeId) ||
                    (log.AppUser.Unit == null && log.AppUser.OfficeId == myOfficeId)
                )));
            }
            else
            {
                return new List<Document>();
            }
            return await query.OrderByDescending(d => d.LastActionDate ?? d.CreatedAt).ToListAsync();

        }

        [HttpGet("history/unit/{unitId}")]
        public async Task<ActionResult<IEnumerable<Document>>> GetUnitHistory(int unitId)
        {
            var document = await _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.NextOffice)
                .Include(d => d.CurrentOffice)
                .Include(d => d.NextOffice)
                .Include(d => d.CurrentOffice)
                .Where(d => _context.DocumentLogs.Any(log => log.DocumentId == d.Id && log.UnitId == unitId))
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            return Ok(document);
        }

        [HttpGet("history/office/{officeId}")]
        public async Task<ActionResult<IEnumerable<Document>>> GetOfficeHistory(int officeId)
        {
            var document = await _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.NextOffice)
                .Include(d => d.CurrentOffice)
                .Include(d => d.NextUnit)
                .Include(d => d.CurrentUnit)
                .Where(d => _context.DocumentLogs.Any(log => log.DocumentId == d.Id &&
                (_context.Units.Any(u => u.Id == log.UnitId && u.OfficeId == officeId) ||
                (log.UnitId == null && log.OfficeId == officeId))))
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            return Ok(document);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDocument(int id, [FromBody] Document doc)
        {
            var existing = await _context.Documents.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Name = doc.Name;
            existing.Type = doc.Type;
            existing.Description = doc.Description;
            existing.Priority = doc.Priority;

            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            var doc = await _context.Documents.FindAsync(id);
            if (doc == null) return NotFound();

            var logs = _context.DocumentLogs.Where(d => d.DocumentId == id);
            _context.DocumentLogs.RemoveRange(logs);
            _context.Documents.Remove(doc);
            await _context.SaveChangesAsync();
            return Ok();
        }

    }
        public class ForwardRequest
        {
            public int NextOfficeId { get; set; }
            public int? NextUnitId { get; set; }
        }
}
