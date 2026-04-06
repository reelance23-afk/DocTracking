using DocTracking.Client.Models;
using DocTracking.Data;
using DocTracking.Hubs;
using DocTracking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace DocTracking.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [EnableRateLimiting("api")]
    public class DocumentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly ILogger<DocumentsController> _logger;
        private readonly DocumentQueryService _docService;

        public DocumentsController(ApplicationDbContext context, IWebHostEnvironment env,
            IHubContext<NotificationHub> hub, ILogger<DocumentsController> logger,
            DocumentQueryService docService)
        {
            _context = context;
            _env = env;
            _hub = hub;
            _logger = logger;
            _docService = docService;
        }

        #region Helpers

        private async Task Notify(string group, string message, string docName)
        {
            try
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
            catch (Exception ex) { _logger.LogWarning(ex, "[Notify] Failed for group {Group}", group); }
        }

        private async Task NotifyOfficeOrUnit(int? unitId, int? officeId, string unitMsg, string officeMsg, string headMsg, string docName)
        {
            if (unitId.HasValue)
            {
                await Notify($"unit-{unitId}", unitMsg, docName);
                await Notify($"office-head-{officeId}", headMsg, docName);
            }
            else if (officeId.HasValue)
            {
                await Notify($"office-{officeId}", officeMsg, docName);
                await Notify($"office-head-{officeId}", headMsg, docName);
            }
        }

        #endregion

        #region Profile

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

        #endregion

        #region Document Queries (Admin / Office)

        [HttpGet]
        [Authorize(Roles = "Admin,Office")]
        public async Task<ActionResult<PagedResult<Document>>> GetAllDocument(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] string? office = null,
            [FromQuery] string? dateFrom = null,
            [FromQuery] string? dateTo = null)
        {
            var (items, total) = await _docService.GetAllDocumentsAsync(page, pageSize, search, status, office, dateFrom, dateTo);
            return Ok(new PagedResult<Document> { Items = items, TotalCount = total });
        }

        [HttpGet("dashboard-stats")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<AdminDashboardStats>> GetDashboardStats()
        {
            var stats = await _docService.GetDashboardStatsAsync();
            return Ok(stats);
        }

        [HttpGet("activity/user/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<Document>>> GetUserActivity(int userId)
        {
            try
            {
                var docIds = await _context.DocumentLogs
                    .Where(l => l.AppUserId == userId)
                    .Select(l => l.DocumentId)
                    .Distinct()
                    .ToListAsync();

                var created = await _context.Documents
                    .Where(d => d.CreatorId == userId)
                    .Select(d => d.Id)
                    .ToListAsync();

                var allIds = docIds.Union(created).Distinct();

                return await _context.Documents
                    .Where(d => allIds.Contains(d.Id))
                    .Include(d => d.Creator)
                    .Include(d => d.CurrentOffice)
                    .Include(d => d.CurrentUnit)
                    .Include(d => d.NextOffice)
                    .Include(d => d.NextUnit)
                    .OrderByDescending(d => d.LastActionDate ?? d.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetUserActivity] Failed for userId {UserId}", userId);
                return StatusCode(500, "Failed to load user activity.");
            }
        }

        [HttpGet("export-csv")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportDocumentsCsv(
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] string? office = null,
            [FromQuery] string? dateFrom = null,
            [FromQuery] string? dateTo = null)
        {
            try
            {
                Response.ContentType = "text/csv";
                Response.Headers.Append("Content-Disposition", $"attachment; filename=documents_{DateTime.Now:yyyyMMdd}.csv");

                var sb = new StringBuilder();
                sb.AppendLine("Name,Type,Sender,Status,Priority,Current Office,Current Unit,Next Office,Created At");

                await foreach (var doc in _docService.StreamAllDocumentsAsync(search, status, office, dateFrom, dateTo))
                {
                    sb.AppendLine($"\"{doc.Name}\",\"{doc.Type}\",\"{doc.Creator?.Name}\",\"{doc.Status}\"," +
                                  $"\"{doc.Priority}\",\"{doc.CurrentOffice?.Name}\",\"{doc.CurrentUnit?.Name}\"," +
                                  $"\"{doc.NextOffice?.Name}\",\"{doc.CreatedAt.ToLocalTime():yyyy-MM-dd}\"");
                }

                return Content(sb.ToString(), "text/csv", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExportDocumentsCsv] Failed");
                return StatusCode(500, "Failed to export documents.");
            }
        }

        #endregion

        #region Document Queries (User)

        [HttpGet("user/{email}")]
        public async Task<ActionResult<PagedResult<Document>>> GetUserDocument(
            string email,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] string? priority = null,
            [FromQuery] string? type = null)
        {
            var (items, total) = await _docService.GetUserDocumentsAsync(email, page, pageSize, search, status, priority, type);
            return Ok(new PagedResult<Document> { Items = items, TotalCount = total });
        }

        [HttpGet("user/{email}/stats")]
        public async Task<ActionResult> GetUserDocumentStats(string email)
        {
            var stats = await _docService.GetUserDocumentStatsAsync(email);
            return Ok(stats);
        }

        [HttpGet("user/{email}/home-data")]
        public async Task<ActionResult<UserHomeData>> GetUserHomeData(string email)
        {
            var stats = await _docService.GetUserHomeDataAsync(email);
            return Ok(stats);
        }

        #endregion

        #region Office Queues

        [HttpGet("incoming/{officeId}")]
        public async Task<ActionResult<IEnumerable<Document>>> GetIncoming(
            int officeId,
            [FromQuery] int? unitId = null,
            [FromQuery] string? search = null)
        {
            var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
            var appUser = await _context.AppUsers.Include(u => u.Unit).FirstOrDefaultAsync(u => u.Email == email);
            bool isOfficeHead = appUser?.UnitId == null && appUser?.OfficeId == officeId;
            var items = await _docService.GetIncomingAsync(officeId, unitId, isOfficeHead, search);
            return Ok(items);
        }

        [HttpGet("desk/{officeId}")]
        public async Task<ActionResult<IEnumerable<Document>>> GetDeskDocuments(
            int officeId,
            [FromQuery] int? unitId = null,
            [FromQuery] string? search = null)
        {
            var items = await _docService.GetDeskDocumentsAsync(officeId, unitId, search);
            return Ok(items);
        }

        [HttpGet("outgoing/user")]
        public async Task<ActionResult<IEnumerable<Document>>> GetOutGoing(
            [FromQuery] string? search = null)
        {
            var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
            var appUser = await _context.AppUsers.Include(u => u.Unit).FirstOrDefaultAsync(u => u.Email == email);
            if (appUser == null) return NotFound();

            int? myOfficeId = appUser.Unit?.OfficeId ?? appUser.OfficeId;
            if (myOfficeId == null) return Ok(new List<Document>());

            var items = await _docService.GetOutgoingAsync(appUser.UnitId, myOfficeId, search);
            return Ok(items);
        }

        #endregion

        #region History

        [HttpGet("history/unit/{unitId}")]
        public async Task<ActionResult<PagedResult<Document>>> GetUnitHistory(
            int unitId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? search = null)
        {
            var (items, total) = await _docService.GetUnitHistoryAsync(unitId, page, pageSize, search);
            return Ok(new PagedResult<Document> { Items = items, TotalCount = total });
        }

        [HttpGet("history/office/{officeId}")]
        public async Task<ActionResult<PagedResult<Document>>> GetOfficeHistory(
            int officeId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? search = null)
        {
            var (items, total) = await _docService.GetOfficeHistoryAsync(officeId, page, pageSize, search);
            return Ok(new PagedResult<Document> { Items = items, TotalCount = total });
        }

        #endregion

        #region Stats

        [HttpGet("stats/unit/{unitId}")]
        public async Task<ActionResult<LocationDocStats>> GetUnitDocStats(int unitId)
        {
            try
            {
                var stats = await _docService.GetLocationDocStatAsync(unitId, null);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetUnitDocStats] Failed");
                return StatusCode(500, "Failed to unit stats");
            }
        }

        [HttpGet("stats/office/{officeId}")]
        public async Task<ActionResult<LocationDocStats>> GetOfficeDocStats(int officeId)
        {
            try
            {
                var stats = await _docService.GetLocationDocStatAsync(null, officeId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetOfficeDocStats] Failed");
                return StatusCode(500, "Failed to load office stats");
            }
        }

        #endregion

        #region Public / Anonymous

        [HttpGet("by-ref/{referenceNumber}")]
        [AllowAnonymous]
        public async Task<ActionResult<Document>> GetByReferenceNumber(string referenceNumber)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetByReferenceNumber] Failed for ref {Ref}", referenceNumber);
                return StatusCode(500, "Failed to load document.");
            }
        }

        [HttpGet("by-ref/{referenceNumber}/context")]
        [AllowAnonymous]
        public async Task<ActionResult> GetDocumentContext(string referenceNumber)
        {
            try
            {
                var doc = await _context.Documents
                    .Include(d => d.Creator)
                    .FirstOrDefaultAsync(d => d.ReferenceNumber == referenceNumber);
                if (doc == null) return NotFound();

                var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
                var officeIdClaim = User.FindFirst("OfficeId")?.Value;
                var unitIdClaim = User.FindFirst("UnitId")?.Value;
                bool isAdmin = User.IsInRole("Admin");
                bool isOfficeHead = User.FindFirst("IsOfficeHead")?.Value == "True";
                int.TryParse(officeIdClaim, out var myOfficeId);
                int.TryParse(unitIdClaim, out var myUnitId);

                if (doc.Creator?.Email == email)
                    return Ok(new { RedirectTo = "my-tracking" });

                if (isAdmin)
                    return Ok(new { RedirectTo = "document-tracking" });

                if (myOfficeId != 0)
                {
                    if (doc.Status == "In Motion" && doc.NextOfficeId == myOfficeId)
                    {
                        bool unitMatch = string.IsNullOrEmpty(unitIdClaim)
                            ? !doc.NextUnitId.HasValue
                            : !doc.NextUnitId.HasValue || doc.NextUnitId == myUnitId;

                        if (unitMatch || isOfficeHead)
                            return Ok(new { RedirectTo = "office-desk", Tab = "Incoming" });
                    }

                    if (doc.CurrentOfficeId == myOfficeId &&
                        (isOfficeHead || !doc.CurrentUnitId.HasValue || doc.CurrentUnitId == myUnitId))
                        return Ok(new { RedirectTo = "office-desk", Tab = "OnDesk" });

                    var isSender = await _context.DocumentLogs
                        .AnyAsync(l => l.DocumentId == doc.Id && l.Action == "Forwarded" && l.OfficeId == myOfficeId);
                    if (isSender)
                        return Ok(new { RedirectTo = "office-desk", Tab = "Outgoing" });

                    var hasHistory = await _context.DocumentLogs
                        .AnyAsync(l => l.DocumentId == doc.Id && l.OfficeId == myOfficeId);
                    if (hasHistory)
                        return Ok(new { RedirectTo = "unit-history" });
                }

                return Ok(new { RedirectTo = "public" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetDocumentContext] Failed for ref {Ref}", referenceNumber);
                return StatusCode(500, "Failed to load document context.");
            }
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

        #endregion

        #region File Management

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null) return BadRequest("No File Uploaded");
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".png" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext)) return BadRequest("File type not allowed.");
            if (file.Length > 10 * 1024 * 1024) return BadRequest("File exceeds 10MB limit.");

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            if (!System.IO.Directory.Exists(uploadsFolder)) System.IO.Directory.CreateDirectory(uploadsFolder);
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            try
            {
                using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UploadFile] Failed");
                return StatusCode(500, "Failed to save file.");
            }
            return Ok(new { FilePath = $"/api/documents/file/{uniqueFileName}" });
        }

        [HttpGet("file/{fileName}")]
        public IActionResult DownloadFile(string fileName)
        {
            if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
                return BadRequest("Invalid file name.");

            var uploadsFolder = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads"));
            var fullPath = Path.GetFullPath(Path.Combine(uploadsFolder, fileName));

            if (!fullPath.StartsWith(uploadsFolder + Path.DirectorySeparatorChar))
                return BadRequest("Invalid file name.");

            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            var contentType = ext switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".jpg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/octet-stream"
            };

            return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
        }

        [HttpDelete("upload")]
        public async Task<IActionResult> DeleteUpload([FromQuery] string path)
        {
            var uploadsFolder = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads"));
            var fullPath = Path.GetFullPath(Path.Combine(_env.WebRootPath, path.TrimStart('/')));
            if (!fullPath.StartsWith(uploadsFolder + Path.DirectorySeparatorChar))
                return BadRequest("Invalid path.");

            var fileName = Path.GetFileName(fullPath);
            var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin)
            {
                var appUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Email == email);
                if (appUser == null) return Forbid();
                var ownsFile = await _context.Documents.AnyAsync(d =>
                    d.CreatorId == appUser.Id &&
                    (d.FilePath == path || d.FilePath == $"/api/documents/file/{fileName}"));
                if (!ownsFile) return Forbid();
            }

            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
            return Ok();
        }

        #endregion

        #region Document Lifecycle

        [HttpPost]
        public async Task<ActionResult<Document>> CreateDocument([FromBody] Document doc)
        {
            if (!await _context.Offices.AnyAsync(o => o.Id == doc.NextOfficeId))
                return BadRequest("Destination office does not exist.");

            if (doc.NextUnitId.HasValue)
            {
                var unitBelongs = await _context.Units.AnyAsync(u => u.Id == doc.NextUnitId && u.OfficeId == doc.NextOfficeId);
                if (!unitBelongs) return BadRequest("Unit does not belong to the specified office.");
            }

            if (string.IsNullOrEmpty(doc.FilePath))
                return BadRequest("A file attachment is required.");

            var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
            var appuser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Email == email);

            doc.CreatedAt = DateTime.UtcNow;
            doc.Status = "In Motion";
            doc.CreatorId = appuser?.Id;

            string randomNum = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
            doc.ReferenceNumber = $"DOC-{DateTime.UtcNow:yyyyMMdd}-{randomNum}";
            doc.LastActionDate = DateTime.UtcNow;

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
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
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "[CreateDocument] Failed");
                return StatusCode(500, "Failed to create document.");
            }

            var creatorName = appuser?.Name ?? "Someone";

            if (appuser != null)
                await Notify($"user-{appuser.Id}", "You created a new document.", doc.Name ?? "");

            await NotifyOfficeOrUnit(doc.NextUnitId, doc.NextOfficeId,
                $"A document from {creatorName} is incoming to your unit",
                $"A document from {creatorName} incoming to your office",
                $"A document from {creatorName} incoming to your office",
                doc.Name ?? "");

            return Ok(doc);
        }

        [HttpPut("{id}/receive")]
        public async Task<IActionResult> ReceiveDocument(int id)
        {
            var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
            var appUser = await _context.AppUsers.Include(u => u.Unit).FirstOrDefaultAsync(u => u.Email == email);

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
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
                await tx.CommitAsync();

                var docName = doc.Name ?? "";
                var receivedBy = appUser?.Name ?? "Someone";

                if (doc.CreatorId.HasValue)
                {
                    if (receivedUnitId.HasValue)
                        await Notify($"user-{doc.CreatorId}", $"Your document has been received by {receivedBy} in {receivedUnitName} of {receivedOfficeName}.", docName);
                    else if (receivedOfficeId.HasValue)
                        await Notify($"user-{doc.CreatorId}", $"Your document has been received by {receivedBy} in {receivedOfficeName}.", docName);
                }

                await NotifyOfficeOrUnit(receivedUnitId, receivedOfficeId,
                    $"{receivedBy} received a document in your unit.",
                    $"{receivedBy} received a document.",
                    $"{receivedBy} received a document in {receivedUnitName ?? receivedOfficeName} of {receivedOfficeName}.",
                    docName);

                return Ok();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "[ReceiveDocument] Failed");
                return StatusCode(500, "Failed to receive document.");
            }
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

            if (!await _context.Offices.AnyAsync(o => o.Id == request.NextOfficeId))
                return BadRequest("Destination office does not exist.");

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

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
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
                await tx.CommitAsync();

                var docName = doc.Name ?? "";
                var forwardedBy = appUser?.Name ?? "Someone";

                if (doc.CreatorId.HasValue)
                {
                    if (request.NextUnitId.HasValue)
                        await Notify($"user-{doc.CreatorId}", $"Your document was forwarded by {forwardedBy} to {nextUnit?.Name} of {nextOffice?.Name}.", docName);
                    else
                        await Notify($"user-{doc.CreatorId}", $"Your document was forwarded by {forwardedBy} to {nextOffice?.Name}.", docName);
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
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "[ForwardDocument] Failed");
                return StatusCode(500, "Failed to forward document.");
            }
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

            var finishedAtOffice = doc.CurrentOfficeId;
            var finishedAtUnit = doc.CurrentUnitId;
            var finishedOfficeName = doc.CurrentOffice?.Name;
            var finishedUnitName = doc.CurrentUnit?.Name;

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                doc.Status = "Completed";
                doc.LastActionDate = DateTime.UtcNow;
                doc.Comment = request.Comment;
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
                await tx.CommitAsync();

                var docName = doc.Name ?? "";
                var finishedBy = appUser?.Name ?? "Someone";

                if (doc.CreatorId.HasValue)
                    await Notify($"user-{doc.CreatorId}", $"Your document has been completed by {finishedBy}.", docName);

                await NotifyOfficeOrUnit(finishedAtUnit, finishedAtOffice,
                    $"{finishedBy} completed a document in your unit.",
                    $"{finishedBy} completed a document.",
                    $"{finishedBy} completed a document in {finishedUnitName ?? finishedOfficeName} of {finishedOfficeName}.",
                    docName);

                return Ok();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "[FinishDocument] Failed");
                return StatusCode(500, "Failed to complete document.");
            }
        }

        #endregion

        #region Document Editing

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDocument(int id, [FromBody] Document doc)
        {
            var existing = await _context.Documents.FindAsync(id);
            if (existing == null) return NotFound();

            var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
            var appUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Email == email);

            var changes = new List<string>();
            if (existing.Name != doc.Name) changes.Add($"Name: '{existing.Name}' to '{doc.Name}'");
            if (existing.Type != doc.Type) changes.Add($"Type: '{existing.Type}' to '{doc.Type}'");
            if (existing.Priority != doc.Priority) changes.Add($"Priority: '{existing.Priority}' to '{doc.Priority}'");
            if (existing.Description != doc.Description) changes.Add("Description updated");
            if (existing.FilePath != doc.FilePath) changes.Add("Attachment replaced");

            existing.Name = doc.Name;
            existing.Type = doc.Type;
            existing.Description = doc.Description;
            existing.Priority = doc.Priority;
            existing.FileName = doc.FileName;
            existing.FilePath = doc.FilePath;

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                if (changes.Any())
                {
                    _context.DocumentLogs.Add(new DocumentLog
                    {
                        DocumentId = existing.Id,
                        Action = "Edited",
                        AppUserId = appUser?.Id,
                        TimeStamp = DateTime.UtcNow,
                        Comment = string.Join("; ", changes)
                    });
                }
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "[UpdateDocument] Failed");
                return StatusCode(500, "Failed to update document.");
            }
            return Ok(existing);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            var doc = await _context.Documents.FindAsync(id);
            if (doc == null) return NotFound();

            var logs = await _context.DocumentLogs.Where(d => d.DocumentId == id).ToListAsync();

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.DocumentLogs.RemoveRange(logs);
                _context.Documents.Remove(doc);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "[DeleteDocument] Failed");
                return StatusCode(500, "Failed to delete document.");
            }
            return Ok();
        }

        [HttpDelete("bulk")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BulkDeleteDocuments([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any()) return BadRequest("No document IDs provided.");

            var docs = await _context.Documents.Where(d => ids.Contains(d.Id)).ToListAsync();
            if (!docs.Any()) return NotFound();

            var logs = await _context.DocumentLogs.Where(l => ids.Contains(l.DocumentId)).ToListAsync();

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.DocumentLogs.RemoveRange(logs);
                _context.Documents.RemoveRange(docs);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "[BulkDeleteDocuments] Failed");
                return StatusCode(500, "Failed to delete documents.");
            }
            return Ok(new { deleted = docs.Count });
        }

        #endregion

        #region Admin Override

        [HttpPut("{id}/admin-override")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminOverride(int id, [FromBody] AdminOverrideRequest request)
        {
            var doc = await _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.CurrentOffice)
                .Include(d => d.NextOffice)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();

            var email = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
            var appUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Email == email);

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var beforeStatus = doc.Status;
                doc.LastActionDate = DateTime.UtcNow;

                if (request.NextOfficeId.HasValue)
                {
                    var explicitStatus = request.Status;

                    if (explicitStatus == "Received")
                    {
                        doc.CurrentOfficeId = request.NextOfficeId;
                        doc.CurrentUnitId = request.NextUnitId;
                        doc.NextOfficeId = null;
                        doc.NextUnitId = null;
                        doc.Status = "Received";
                    }
                    else if (explicitStatus == "Completed")
                    {
                        doc.CurrentOfficeId = null;
                        doc.CurrentUnitId = null;
                        doc.NextOfficeId = null;
                        doc.NextUnitId = null;
                        doc.Status = "Completed";
                    }
                    else
                    {
                        doc.CurrentOfficeId = null;
                        doc.CurrentUnitId = null;
                        doc.NextOfficeId = request.NextOfficeId;
                        doc.NextUnitId = request.NextUnitId;
                        doc.Status = "In Motion";
                    }
                }
                else
                {
                    doc.Status = request.Status ?? doc.Status;
                }

                var routedOffice = await _context.Offices.FindAsync(request.NextOfficeId);
                var routedUnit = request.NextUnitId.HasValue ? await _context.Units.FindAsync(request.NextUnitId) : null;

                var snapshotParts = new List<string> { $"Status: '{beforeStatus}' to '{doc.Status}'" };
                if (request.NextOfficeId.HasValue)
                    snapshotParts.Add($"Routed to {routedOffice?.Name ?? $"Office {request.NextOfficeId}"}" +
                        (routedUnit != null ? $" / {routedUnit.Name}" : ""));
                var userComment = request.NextOfficeId.HasValue ? request.ReassignComment : request.ForceComment;
                if (!string.IsNullOrEmpty(userComment))
                    snapshotParts.Add($"Reason: {userComment}");

                _context.DocumentLogs.Add(new DocumentLog
                {
                    DocumentId = doc.Id,
                    Action = "Admin Override",
                    OfficeId = request.NextOfficeId ?? doc.CurrentOfficeId,
                    UnitId = request.NextUnitId ?? doc.CurrentUnitId,
                    AppUserId = appUser?.Id,
                    TimeStamp = DateTime.UtcNow,
                    Comment = string.Join(" | ", snapshotParts)
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                if (doc.CreatorId.HasValue)
                    await Notify($"user-{doc.CreatorId}", "Your document status was updated by an admin.", doc.Name ?? "");

                return Ok();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "[AdminOverride] Failed");
                return StatusCode(500, "Failed to apply admin override.");
            }
        }

        #endregion
    }

    #region Request Models

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

    public class AdminOverrideRequest
    {
        public string? Status { get; set; }
        public string? ForceComment { get; set; }
        public int? NextOfficeId { get; set; }
        public int? NextUnitId { get; set; }
        public string? ReassignComment { get; set; }
    }

    #endregion
}
