using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
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
    public class DocumentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<NotificationHub> _hub;

        public DocumentsController(ApplicationDbContext context, IWebHostEnvironment env,
            IHubContext<NotificationHub> hub)
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

        private async Task TryNotify(string group, string message, string docName)
        {
            try { await Notify(group, message, docName); }
            catch (Exception ex) { Console.WriteLine($"[Notify] Failed for group {group}: {ex.Message}"); }
        }

        [HttpPost]
        public async Task<ActionResult<Document>> CreateDocument([FromBody] Document doc)
        {
            if (!await _context.Offices.AnyAsync(o => o.Id == doc.NextOfficeId))
                return BadRequest("Destination office does not exist.");

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
                UnitId = doc.NextUnitId,
                OfficeId = doc.NextOfficeId,
                AppUserId = appuser?.Id
            });
            await _context.SaveChangesAsync();

            var creatorName = appuser?.Name ?? "Someone";

            if (appuser != null)
                await TryNotify($"user-{appuser.Id}", "You created a new document.", doc.Name ?? "");

            await NotifyOfficeOrUnit(doc.NextUnitId, doc.NextOfficeId,
            $"A document from {creatorName} is incoming to your unit",
            $"A document from {creatorName} incoming to your office",
            $"A document from {creatorName} incoming to your office",
             doc.Name ?? "");


            return Ok(doc);
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null) return BadRequest("No File Uploaded");
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".png" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext)) return BadRequest("File type not allowed.");
            if (file.Length > 10 * 1024 * 1024) return BadRequest("File exceeds 10MB limit.");

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);
            return Ok(new { FilePath = $"/uploads/{uniqueFileName}" });
        }

        [HttpDelete("upload")]
        public IActionResult DeleteUpload([FromQuery] string path)
        {
            var uploadsFolder = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads"));
            var fullPath = Path.GetFullPath(Path.Combine(_env.WebRootPath, path.TrimStart('/')));
            if (!fullPath.StartsWith(uploadsFolder + Path.DirectorySeparatorChar))
                return BadRequest("Invalid path.");
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
            bool isOfficeHead = appUser?.UnitId == null && appUser?.OfficeId == officeId;

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

        [HttpGet("desk/{officeId}")]
        public async Task<ActionResult<IEnumerable<Document>>> GetDeskDocuments(int officeId, [FromQuery] int? unitId = null)
        {
            var query = _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.CurrentOffice)
                .Include(d => d.CurrentUnit)
                .Where(d => d.CurrentOfficeId == officeId && d.Status == "Received");

            if (unitId.HasValue)
                query = query.Where(d => d.CurrentUnitId == null || d.CurrentUnitId == unitId);
            else
                query = query.Where(d => d.CurrentUnitId == null);

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
            var appUser = await _context.AppUsers.Include(u => u.Unit).FirstOrDefaultAsync(u => u.Email == email);
            int? callerOfficeId = appUser?.Unit?.OfficeId ?? appUser?.OfficeId;
            if (callerOfficeId != doc.NextOfficeId)
                return Forbid();

            var receivedOfficeId = doc.NextOfficeId;
            var receivedUnitId = doc.NextUnitId;
            var receivedOfficeName = doc.NextOffice?.Name;
            var receivedUnitName = doc.NextUnit?.Name;

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
                TimeStamp = DateTime.UtcNow,
                Comment = doc.Comment
            });

            await _context.SaveChangesAsync();

            var docName = doc.Name ?? "";
            var receivedBy = appUser?.Name ?? "Someone";

            if (doc.CreatorId.HasValue)
            {
                if (receivedUnitId.HasValue)
                    await TryNotify($"user-{doc.CreatorId}", $"Your document has been received by {receivedBy} in {receivedUnitName} of {receivedOfficeName}.", docName);
                else if (receivedOfficeId.HasValue)
                    await TryNotify($"user-{doc.CreatorId}", $"Your document has been received by {receivedBy} in {receivedOfficeName}.", docName);
            }

            await NotifyOfficeOrUnit(receivedUnitId, receivedOfficeId,
            $"{receivedBy} received a document in your unit.",
            $"{receivedBy} received a document.",
            $"{receivedBy} received a document in {receivedUnitName ?? receivedOfficeName} of {receivedOfficeName}.",
            docName);


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
            int? callerOfficeId = appUser?.Unit?.OfficeId ?? appUser?.OfficeId;
            if (callerOfficeId != doc.CurrentOfficeId)
                return Forbid();

            var fromOfficeId = doc.CurrentOfficeId;
            var fromUnitId = doc.CurrentUnitId;
            var fromOfficeName = doc.CurrentOffice?.Name ?? "an office";
            var fromUnitName = doc.CurrentUnit?.Name;

            var nextOffice = await _context.Offices.FindAsync(request.NextOfficeId);
            var nextUnit = request.NextUnitId.HasValue ? await _context.Units.FindAsync(request.NextUnitId) : null;
            var destination = nextUnit != null ? nextUnit.Name : nextOffice?.Name ?? "another office";

            doc.Status = "In Motion";
            doc.Comment = request.Comment;
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
                TimeStamp = DateTime.UtcNow,
                Comment = request.Comment
            });

            await _context.SaveChangesAsync();

            var docName = doc.Name ?? "";
            var forwardedBy = appUser?.Name ?? "Someone";

            if (doc.CreatorId.HasValue)
            {
                if (request.NextUnitId.HasValue)
                    await TryNotify($"user-{doc.CreatorId}", $"Your document was forwarded by {forwardedBy} to {nextUnit?.Name} of {nextOffice?.Name}.", docName);
                else
                    await TryNotify($"user-{doc.CreatorId}", $"Your document was forwarded by {forwardedBy} to {nextOffice?.Name}.", docName);
            }

            await NotifyOfficeOrUnit(fromUnitId, fromOfficeId,
            $"{forwardedBy} forwarded a document to {destination}.",
            $"{forwardedBy} forwarded a document to {destination}.",
            $"{forwardedBy} forwarded a document from {fromUnitName ?? fromOfficeName} of {fromOfficeName} to {destination}.",
            docName);

            await NotifyOfficeOrUnit(request.NextUnitId, request.NextOfficeId,
            $"{forwardedBy} forwarded a document to your unit.",
            $"{forwardedBy} forwarded a document to your office.",
            $"{forwardedBy} forwarded a document to {nextUnit?.Name ?? nextOffice?.Name}.",
            docName);

            return Ok();
        }

        [HttpPut("{id}/finish")]
        public async Task<IActionResult> FinishDocument(int id, [FromBody] FinishRequest request)
        {
            var doc = await _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.CurrentOffice)
                .Include(d => d.CurrentUnit)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();

            var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
            var appUser = await _context.AppUsers.Include(u => u.Unit).FirstOrDefaultAsync(u => u.Email == email);
            int? callerOfficeId = appUser?.Unit?.OfficeId ?? appUser?.OfficeId;
            if (callerOfficeId != doc.CurrentOfficeId)
                return Forbid();

            doc.Status = "Completed";
            doc.LastActionDate = DateTime.UtcNow;
            doc.Comment = request.Comment;

            var finishedAtOffice = doc.CurrentOfficeId;
            var finishedAtUnit = doc.CurrentUnitId;
            var finishedOfficeName = doc.CurrentOffice?.Name;
            var finishedUnitName = doc.CurrentUnit?.Name;

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
                TimeStamp = DateTime.UtcNow,
                Comment = request.Comment
            });

            await _context.SaveChangesAsync();

            var docName = doc.Name ?? "";
            var finishedBy = appUser?.Name ?? "Someone";

            if (doc.CreatorId.HasValue)
                await TryNotify($"user-{doc.CreatorId}", $"Your document has been completed by {finishedBy}.", docName);

            await NotifyOfficeOrUnit(finishedAtUnit, finishedAtOffice,
            $"{finishedBy} completed a document in your unit.",
            $"{finishedBy} completed a document.",
            $"{finishedBy} completed a document in {finishedUnitName ?? finishedOfficeName} of {finishedOfficeName}.",
            docName);

            return Ok();
        }

        [HttpGet("outgoing/user")]
        public async Task<ActionResult<IEnumerable<Document>>> GetOutGoing()
        {
            var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
            var appUser = await _context.AppUsers.Include(u => u.Unit).FirstOrDefaultAsync(u => u.Email == email);
            if (appUser == null) return NotFound();

            int? myOfficeId = appUser.Unit?.OfficeId ?? appUser.OfficeId;
            int? myUnitId = appUser.UnitId;

            if (myOfficeId == null) return new List<Document>();

            IQueryable<int> forwardedDocIds;

            if (myUnitId.HasValue)
            {
                forwardedDocIds = _context.DocumentLogs
                    .Where(log => log.Action == "Forwarded" && log.AppUser != null && log.AppUser.UnitId == myUnitId)
                    .Select(log => log.DocumentId)
                    .Distinct();
            }
            else
            {
                forwardedDocIds = _context.DocumentLogs
                    .Where(log => log.Action == "Forwarded" && log.AppUser != null &&
                        ((log.AppUser.Unit != null && log.AppUser.Unit.OfficeId == myOfficeId) ||
                         (log.AppUser.Unit == null && log.AppUser.OfficeId == myOfficeId)))
                    .Select(log => log.DocumentId)
                    .Distinct();
            }

            return await _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.NextOffice)
                .Include(d => d.NextUnit)
                .Where(d => d.Status == "In Motion" && forwardedDocIds.Contains(d.Id))
                .OrderByDescending(d => d.LastActionDate ?? d.CreatedAt)
                .ToListAsync();
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

        [HttpGet("{id}/qrcode")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDocumentQRCode(int id)
        {
            var doc = await _context.Documents.FindAsync(id);
            if (doc == null || string.IsNullOrEmpty(doc.ReferenceNumber)) return NotFound();

            var request = HttpContext.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";
            var trackUrl = $"{baseUrl}/track/{doc.ReferenceNumber}";

            using var qrGenerator = new QRCoder.QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(trackUrl, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
            var qrCodeBytes = qrCode.GetGraphic(20);

            return File(qrCodeBytes, "image/png");
        }

        [HttpGet("by-ref/{referenceNumber}")]
        [AllowAnonymous]
        public async Task<ActionResult<Document>> GetByReferenceNumber(string referenceNumber)
        {
            var doc = await _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.NextOffice)
                .Include(d => d.CurrentOffice)
                .Include(d => d.NextUnit)
                .Include(d => d.CurrentUnit)
                .FirstOrDefaultAsync(d => d.ReferenceNumber == referenceNumber);
            return doc == null ? NotFound() : Ok(doc);
        }

        private async Task NotifyOfficeOrUnit(int? unitId, int? officeId,string unitMsg, string officeMsg, string headMsg, string docName)
        {
            if (unitId.HasValue)
            {
                await TryNotify($"unit-{unitId}", unitMsg, docName);
                await TryNotify($"office-head-{officeId}", headMsg, docName);
            }
            else if (officeId.HasValue)
            {
                await TryNotify($"office-{officeId}", officeMsg, docName);
                await TryNotify($"office-head-{officeId}", headMsg, docName);
            }
        }
    }

    public class ForwardRequest
    {
        public int NextOfficeId { get; set; }
        public int? NextUnitId { get; set; }
        public string? Comment { get; set; }
    }

    public class FinishRequest
    {
        public string? Comment { get; set; }
    }
}
