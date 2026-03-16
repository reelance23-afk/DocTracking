using DocTracking.Client.Models;
using DocTracking.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DocTracking.Components.Pages
{
    public class TrackModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public TrackModel(ApplicationDbContext db) => _db = db;

        public Document? Doc { get; set; }
        public List<DocumentLog> Logs { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string referenceNumber)
        {
            Doc = await _db.Documents
                .Include(d => d.Creator)
                .Include(d => d.CurrentOffice)
                .Include(d => d.CurrentUnit)
                .Include(d => d.NextOffice)
                .Include(d => d.NextUnit)
                .FirstOrDefaultAsync(d => d.ReferenceNumber == referenceNumber);

            if (Doc == null) return Page();

            if (User.Identity?.IsAuthenticated == true)
            {
                var email = User.Identity.Name ?? User.FindFirst(ClaimTypes.Email)?.Value;
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var officeIdClaim = User.FindFirst("OfficeId")?.Value;
                var unitIdClaim = User.FindFirst("UnitId")?.Value;
                bool isAdmin = User.IsInRole("Admin");
                bool isOfficeHead = User.FindFirst("IsOfficeHead")?.Value == "True";
                int.TryParse(officeIdClaim, out var myOfficeId);
                int.TryParse(unitIdClaim, out var myUnitId);

                if (isAdmin)
                    return Redirect($"/document-tracking?ref={referenceNumber}");

                if (myOfficeId != 0)
                {
                    var logs2 = await _db.DocumentLogs
                        .Include(l => l.AppUser)
                        .Where(l => l.DocumentId == Doc.Id)
                        .OrderByDescending(l => l.TimeStamp)
                        .ToListAsync();

                    bool isIncomingToMe = Doc.Status == "In Motion" &&
                                         Doc.NextOfficeId == myOfficeId &&
                                         (isOfficeHead || !Doc.NextUnitId.HasValue || Doc.NextUnitId == myUnitId);

                    if (isIncomingToMe)
                        return Redirect($"/office-desk?ref={referenceNumber}&tab=Incoming");

                    if (Doc.Creator?.Email == email)
                        return Redirect($"/my-tracking?ref={referenceNumber}");

                    var lastForward = logs2.FirstOrDefault(l => l.Action == "Forwarded");
                    bool isRecipient = Doc.NextOfficeId == myOfficeId &&
                                       (isOfficeHead || !Doc.NextUnitId.HasValue || Doc.NextUnitId == myUnitId);
                    bool isSender = lastForward?.OfficeId == myOfficeId;
                    bool isOnDesk = Doc.CurrentOfficeId == myOfficeId &&
                                    (isOfficeHead || !Doc.CurrentUnitId.HasValue || Doc.CurrentUnitId == myUnitId);

                    if (isSender || isOnDesk)
                        return Redirect($"/office-desk?ref={referenceNumber}&tab={(isOnDesk ? "OnDesk" : "Outgoing")}");
                    else if (!isRecipient && logs2.Any(l => l.OfficeId == myOfficeId))
                        return Redirect($"/unit-history?ref={referenceNumber}");
                }

                if (Doc.Creator?.Email == email)
                    return Redirect($"/my-tracking?ref={referenceNumber}");
            }

            Logs = await _db.DocumentLogs
                .Include(l => l.Office)
                .Include(l => l.Unit)
                .Include(l => l.AppUser)
                .Where(l => l.DocumentId == Doc.Id)
                .OrderByDescending(l => l.TimeStamp)
                .ToListAsync();

            return Page();
        }
    }
}
