using DocTracking.Data;
using DocTracking.Client.Models;
using DocTracking.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace DocTracking.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DocumentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<NotificationHub> _hub;

        public DocumentsController(ApplicationDbContext context, IWebHostEnvironment env, IHubContext<NotificationHub> hub)
        {
            _context = context;
            _env = env;
            _hub = hub;
        }

        private async Task Notify(string group, string message, string docName)
        {
            await _hub.Clients.Group(group).SendAsync("ReceiveNotification", message, docName);

            List<AppUser> targets = new();

            if (group.StartsWith("user-") && int.TryParse(group[5..], out var uid))
                targets = await _context.AppUsers.Where(u => u.Id == uid).ToListAsync();
            else if (group.StartsWith("unit-") && int.TryParse(group[5..], out var unitId))
                targets = await _context.AppUsers.Where(u => u.UnitId == unitId).ToListAsync();
            else if (group.StartsWith("office-head-") && int.TryParse(group[12..], out var ohId))
                targets = await _context.AppUsers.Where(u => u.OfficeId == ohId && u.IsOfficeHead).ToListAsync();
            else if (group.StartsWith("office-") && int.TryParse(group[7..], out var offId))
                targets = await _context.AppUsers.Where(u => u.OfficeId == offId && !u.IsOfficeHead && u.UnitId == null).ToListAsync();

            var now = DateTime.UtcNow;
            _context.AppNotifications.AddRange(targets.Select(u => new AppNotification
            {
                AppUserId = u.Id,
                Message = message,
                DocumentName = docName,
                Time = now
            }));
            await _context.SaveChangesAsync();
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

            _context.DocumentLogs.Add(new DocumentLog
            {
                DocumentId = doc.Id,
                Action = "Created",
                TimeStamp = DateTime.UtcNow,
                OfficeId = doc.NextOfficeId,
                UnitId = doc.NextUnitId,
                AppUserId = appuser?.Id
            });

            await _context.SaveChangesAsync();

            if (appuser != null)
                await Notify($"user-{appuser.Id}", "You created a new document.", doc.Name ?? "");

            if (doc.NextUnitId.HasValue)
            {
                await Notify($"unit-{doc.NextUnitId}", $"A document from {doc.Name} is incoming to your unit", doc.Name ?? "");
                await Notify($"office-head-{doc.NextUnitId}", $"A document from {doc.Name} incoming to one of your units", doc.Name ?? "");
                   
            }
            else if (doc.NextOfficeId.HasValue)
            {
                await Notify($"office-{doc.NextOfficeId}", $"A document from {doc.Name} incoming to your office", doc.Name ?? "");
                await Notify($"office-head-{doc.NextOfficeId}", $"A document from {doc.Name} incoming to your office", doc.Name ?? "");
            }


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
        public async Task<ActionResult<IEnumerable<Document>>> GetIncoming(int officeId, [FromQuery] int? unitId = null)
        {
            var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
            var appUser = await _context.AppUsers.Include(u => u.Unit).FirstOrDefaultAsync(u => u.Email == email);
            bool isOfficeHead = appUser?.UnitId == null & appUser?.OfficeId == officeId;

            var query = _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.NextOffice)
                .Include(d => d.NextUnit)
                .Where(d => d.NextOfficeId == officeId && d.Status == "In Motion");

            if (unitId.HasValue)
                query = query.Where(d => d.NextUnitId == unitId || d.NextUnitId == null);
            else if (!isOfficeHead)
                query = query.Where(d => d.NextUnitId == null);

            return await query.ToListAsync();
        }

        [HttpPut("{id}/receive")]
        public async Task<IActionResult> ReceiveDocument(int id)
        {
            var doc = await _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.CurrentOffice)
                .Include(d => d.NextOffice)
                .Include(d => d.CurrentUnit)
                .Include(d => d.NextUnit)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();
            if (doc.Status != "In Motion") return Conflict("Document has already been Received");

            var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
            var appUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Email == email);

            var receivedOfficeId = doc.NextOfficeId;
            var receivedUnitId = doc.NextUnitId;

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

            var docName = doc.Name ?? "";
            var receivedBy = appUser?.Name ?? "Someone";

            if (doc.CreatorId.HasValue)
            {
                if (receivedUnitId.HasValue)
                    await Notify($"user-{doc.CreatorId}", $"Your document has been received by {receivedBy} in {doc.NextUnit?.Name} of {doc.NextOffice?.Name}.", docName);
                else if (receivedOfficeId.HasValue)
                    await Notify($"user-{doc.CreatorId}", $"Your document has been received by {receivedBy} in {doc.NextOffice?.Name}.", docName);
            }

            if (receivedUnitId.HasValue)
            {
                await Notify($"unit-{receivedUnitId}", $"{receivedBy} received a document in your unit.", docName);
                await Notify($"office-head-{receivedOfficeId}", $"{receivedBy} received a document in {doc.NextUnit?.Name} of {doc.NextOffice?.Name}.", docName);
            }
            else if (receivedOfficeId.HasValue)
            {
                await Notify($"office-{receivedOfficeId}", $"{receivedBy} received a document.", docName);
                await Notify($"office-head-{receivedOfficeId}", $"{receivedBy} received a document in {doc.NextUnit?.Name}.", docName);
            }


            return Ok();
        }

        [HttpPut("{id}/forward")]
        public async Task<IActionResult> ForwardDocument(int id, [FromBody] ForwardRequest request)
        {
            var doc = await _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.CurrentOffice)
                .Include(d => d.NextOffice)
                .Include(d => d.CurrentUnit)
                .Include(d => d.NextUnit)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();
            if (doc.Status != "Received") return Conflict("Document has already been forwarded");

            var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
            var appUser = await _context.AppUsers.Include(u => u.Unit).FirstOrDefaultAsync(u => u.Email == email);

            var fromOfficeId = doc.CurrentOfficeId;
            var fromUnitId = doc.CurrentUnitId;

            var nextOffice = await _context.Offices.FindAsync(request.NextOfficeId);
            var nextUnit = request.NextUnitId.HasValue ? await _context.Units.FindAsync(request.NextUnitId) : null;
            var destination = nextUnit != null ? nextUnit.Name : nextOffice?.Name ?? "another office";

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

            var docName = doc.Name ?? "";
            var forwardedBy = appUser?.Name ?? "Someone";
            var fromOfficeName = doc.CurrentOffice?.Name ?? "an office";
            var fromUnitName = doc.CurrentUnit?.Name;

            if (doc.CreatorId.HasValue)
            {
                if (request.NextUnitId.HasValue)
                    await Notify($"user-{doc.CreatorId}", $"Your document was forwarded by {forwardedBy} to {nextUnit?.Name} of {nextOffice?.Name}.", docName);
                else
                    await Notify($"user-{doc.CreatorId}", $"Your document was forwarded by {forwardedBy} to {nextOffice?.Name}.", docName);
            }

            if (fromUnitId.HasValue)
            {
                await Notify($"unit-{fromUnitId}", $"{forwardedBy} forwarded a document to {destination}.", docName);
                await Notify($"office-head-{fromOfficeId}", $"{forwardedBy} forwarded a document from {fromUnitName} of {fromOfficeName} to {destination}.", docName);
            }
            else if (fromOfficeId.HasValue)
            {
                await Notify($"office-{fromOfficeId}", $"{forwardedBy} forwarded a document to {destination}.", docName);
                await Notify($"office-head-{fromOfficeId}", $"{forwardedBy} forwarded a document from {fromOfficeName} to {destination}.", docName);
            }

            if (request.NextUnitId.HasValue)
            {
                await Notify($"unit-{request.NextUnitId}", $"{forwardedBy} forwarded a document to your unit.", docName);
                await Notify($"office-head-{request.NextOfficeId}", $"{forwardedBy} forwarded a document to {nextUnit?.Name} of {nextOffice?.Name}.", docName);
            }
            else
            {
                await Notify($"office-{request.NextOfficeId}", $"{forwardedBy} forwarded a document to your office.", docName);
                await Notify($"office-head-{request.NextOfficeId}", $"{forwardedBy} forwarded a document to {nextOffice?.Name}.", docName);
            }

            return Ok();
        }

        [HttpPut("{id}/finish")]
        public async Task<IActionResult> FinishDocument(int id)
        {
            var doc = await _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.CurrentOffice)
                .Include(d => d.CurrentUnit)
                .FirstOrDefaultAsync(d => d.Id == id);
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

            var docName = doc.Name ?? "";
            var finishedBy = appUser?.Name ?? "Someone";
            var finishedOfficeName = doc.CurrentOffice?.Name;
            var finishedUnitName = doc.CurrentUnit?.Name;

            if (doc.CreatorId.HasValue)
                await Notify($"user-{doc.CreatorId}", $"Your document has been completed by {finishedBy}.", docName);

            if (finishedAtUnit.HasValue)
            {
                await Notify($"unit-{finishedAtUnit}", $"{finishedBy} completed a document in your unit.", docName);
                await Notify($"office-head-{finishedAtOffice}", $"{finishedBy} completed a document in {finishedUnitName} of {finishedOfficeName}.", docName);
            }
            else if (finishedAtOffice.HasValue)
            {
                await Notify($"office-{finishedAtOffice}", $"{finishedBy} completed a document.", docName);
                await Notify($"office-head-{finishedAtOffice}", $"{finishedBy} completed a document in {finishedOfficeName}.", docName);
            }

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
                .Include(d => d.NextUnit)
                .Include(d => d.CurrentUnit)
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
            existing.FileName = doc.FileName;
            existing.FilePath = doc.FilePath;

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
