using DocTracking.Client.Models;
using DocTracking.Data;
using Microsoft.EntityFrameworkCore;

namespace DocTracking.Services
{
    public class DocumentQueryService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DocumentQueryService> _logger;

        public DocumentQueryService(ApplicationDbContext context, ILogger<DocumentQueryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<(List<Document> Items, int TotalCount)> GetAllDocumentsAsync(
            int page, int pageSize,
            string? search = null, string? status = null,
            string? office = null, string? dateFrom = null, string? dateTo = null)
        {
            try
            {
                var query = _context.Documents
                    .Include(d => d.Creator)
                    .Include(d => d.NextOffice)
                    .Include(d => d.CurrentOffice)
                    .Include(d => d.NextUnit)
                    .Include(d => d.CurrentUnit)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(search))
                    query = query.Where(d =>
                        (d.Name != null && d.Name.Contains(search)) ||
                        (d.ReferenceNumber != null && d.ReferenceNumber.Contains(search)) ||
                        (d.Creator != null && d.Creator.Name != null && d.Creator.Name.Contains(search)));

                if (!string.IsNullOrEmpty(status))
                    query = query.Where(d => d.Status == status);

                if (!string.IsNullOrEmpty(office))
                    query = query.Where(d =>
                        (d.CurrentOffice != null && d.CurrentOffice.Name == office) ||
                        (d.NextOffice != null && d.NextOffice.Name == office));

                if (DateTime.TryParse(dateFrom, out var from))
                    query = query.Where(d => d.CreatedAt >= from);

                if (DateTime.TryParse(dateTo, out var to))
                    query = query.Where(d => d.CreatedAt <= to.AddDays(1));

                var total = await query.CountAsync();
                var items = await query
                    .OrderByDescending(d => d.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return (items, total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetAllDocumentsAsync] Failed");
                return (new List<Document>(), 0);
            }
        }

        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            try
            {
                var totalDocuments = await _context.Documents.CountAsync();
                var totalOffices = await _context.Offices.CountAsync();
                var totalUsers = await _context.AppUsers.CountAsync();
                var inMotionCount = await _context.Documents.CountAsync(d => d.Status == "In Motion");
                var receivedCount = await _context.Documents.CountAsync(d => d.Status == "Received");
                var completedCount = await _context.Documents.CountAsync(d => d.Status == "Completed");

                var workloadCounts = await _context.Documents
                    .Where(d => d.CurrentOfficeId != null)
                    .GroupBy(d => d.CurrentOfficeId)
                    .Select(g => new { OfficeId = g.Key!.Value, Count = g.Count() })
                    .ToListAsync();

                var officeNames = await _context.Offices
                    .Where(o => workloadCounts.Select(w => w.OfficeId).Contains(o.Id))
                    .Select(o => new { o.Id, o.Name })
                    .ToListAsync();

                var officeWorkloads = workloadCounts
                    .Join(officeNames, w => w.OfficeId, o => o.Id,
                        (w, o) => new OfficeWorkload { OfficeId = w.OfficeId, OfficeName = o.Name, DocumentCount = w.Count })
                    .OrderByDescending(w => w.DocumentCount)
                    .ToList();

                var recentDocuments = await _context.Documents
                    .Include(d => d.Creator)
                    .OrderByDescending(d => d.LastActionDate ?? d.CreatedAt)
                    .Take(4)
                    .ToListAsync();

                var inMotionDocs = await _context.Documents
                    .Include(d => d.Creator)
                    .Where(d => d.Status == "In Motion")
                    .OrderByDescending(d => d.LastActionDate ?? d.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                int maxDoc = Math.Max(totalDocuments, 1);
                foreach (var w in officeWorkloads)
                    w.Percentage = (int)Math.Round((double)w.DocumentCount / maxDoc * 100);

                return new DashboardStats
                {
                    TotalDocuments = totalDocuments,
                    TotalOffices = totalOffices,
                    TotalUsers = totalUsers,
                    InMotionCount = inMotionCount,
                    ReceivedCount = receivedCount,
                    CompletedCount = completedCount,
                    OfficeWorkloads = officeWorkloads,
                    RecentDocuments = recentDocuments,
                    InMotionDocs = inMotionDocs
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetDashboardStatsAsync] Failed");
                return new DashboardStats();
            }
        }

        public async Task<List<Document>> GetIncomingAsync(
            int officeId, int? unitId, bool isOfficeHead, string? search = null)
        {
            try
            {
                var query = _context.Documents
                    .Include(d => d.Creator)
                    .Include(d => d.NextOffice)
                    .Include(d => d.NextUnit)
                    .Where(d => d.NextOfficeId == officeId && d.Status == "In Motion");

                if (unitId.HasValue)
                    query = query.Where(d => d.NextUnitId == unitId || d.NextUnitId == null);
                else if (!isOfficeHead)
                    query = query.Where(d => d.NextUnitId == null);

                if (!string.IsNullOrEmpty(search))
                    query = query.Where(d =>
                        (d.Name != null && d.Name.Contains(search)) ||
                        (d.Type != null && d.Type.Contains(search)) ||
                        (d.Creator != null && d.Creator.Name != null && d.Creator.Name.Contains(search)));

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetIncomingAsync] Failed for officeId {OfficeId}", officeId);
                return new List<Document>();
            }
        }

        public async Task<List<Document>> GetDeskDocumentsAsync(
            int officeId, int? unitId, string? search = null)
        {
            try
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

                if (!string.IsNullOrEmpty(search))
                    query = query.Where(d =>
                        (d.Name != null && d.Name.Contains(search)) ||
                        (d.Type != null && d.Type.Contains(search)));

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetDeskDocumentsAsync] Failed for officeId {OfficeId}", officeId);
                return new List<Document>();
            }
        }

        public async Task<List<Document>> GetOutgoingAsync(
            int? unitId, int? officeId, string? search = null)
        {
            try
            {
                List<int> forwardedDocIds;

                if (unitId.HasValue)
                {
                    forwardedDocIds = await _context.DocumentLogs
                        .Where(log => log.Action == "Forwarded" && log.AppUser != null && log.AppUser.UnitId == unitId)
                        .Select(log => log.DocumentId)
                        .Distinct()
                        .ToListAsync();
                }
                else
                {
                    forwardedDocIds = await _context.DocumentLogs
                        .Where(log => log.Action == "Forwarded" && log.AppUser != null &&
                            ((log.AppUser.Unit != null && log.AppUser.Unit.OfficeId == officeId) ||
                             (log.AppUser.Unit == null && log.AppUser.OfficeId == officeId)))
                        .Select(log => log.DocumentId)
                        .Distinct()
                        .ToListAsync();
                }

                var query = _context.Documents
                    .Include(d => d.Creator)
                    .Include(d => d.NextOffice)
                    .Include(d => d.NextUnit)
                    .Where(d => d.Status == "In Motion" && forwardedDocIds.Contains(d.Id));

                if (!string.IsNullOrEmpty(search))
                    query = query.Where(d =>
                        (d.Name != null && d.Name.Contains(search)) ||
                        (d.Type != null && d.Type.Contains(search)) ||
                        (d.NextOffice != null && d.NextOffice.Name != null && d.NextOffice.Name.Contains(search)));

                return await query
                    .OrderByDescending(d => d.LastActionDate ?? d.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetOutgoingAsync] Failed for officeId {OfficeId}", officeId);
                return new List<Document>();
            }
        }

        public async Task<(List<Document> Items, int TotalCount)> GetUnitHistoryAsync(
            int unitId, int page, int pageSize, string? search = null)
        {
            try
            {
                var docIds = await _context.DocumentLogs
                    .Where(l => l.UnitId == unitId)
                    .Select(l => l.DocumentId)
                    .Distinct()
                    .ToListAsync();

                var query = _context.Documents
                    .Include(d => d.Creator)
                    .Include(d => d.NextOffice)
                    .Include(d => d.CurrentOffice)
                    .Include(d => d.NextUnit)
                    .Include(d => d.CurrentUnit)
                    .Where(d => docIds.Contains(d.Id));

                if (!string.IsNullOrEmpty(search))
                    query = query.Where(d =>
                        (d.Name != null && d.Name.Contains(search)) ||
                        (d.Type != null && d.Type.Contains(search)) ||
                        (d.Status != null && d.Status.Contains(search)) ||
                        (d.CurrentOffice != null && d.CurrentOffice.Name != null && d.CurrentOffice.Name.Contains(search)));

                var total = await query.CountAsync();
                var items = await query
                    .OrderByDescending(d => d.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return (items, total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetUnitHistoryAsync] Failed for unitId {UnitId}", unitId);
                return (new List<Document>(), 0);
            }
        }

        public async Task<(List<Document> Items, int TotalCount)> GetOfficeHistoryAsync(
            int officeId, int page, int pageSize, string? search = null)
        {
            try
            {
                var unitIds = await _context.Units
                    .Where(u => u.OfficeId == officeId)
                    .Select(u => u.Id)
                    .ToListAsync();

                var docIds = await _context.DocumentLogs
                    .Where(l => (l.UnitId != null && unitIds.Contains(l.UnitId.Value)) ||
                                (l.UnitId == null && l.OfficeId == officeId))
                    .Select(l => l.DocumentId)
                    .Distinct()
                    .ToListAsync();

                var query = _context.Documents
                    .Include(d => d.Creator)
                    .Include(d => d.NextOffice)
                    .Include(d => d.CurrentOffice)
                    .Include(d => d.NextUnit)
                    .Include(d => d.CurrentUnit)
                    .Where(d => docIds.Contains(d.Id));

                if (!string.IsNullOrEmpty(search))
                    query = query.Where(d =>
                        (d.Name != null && d.Name.Contains(search)) ||
                        (d.Type != null && d.Type.Contains(search)) ||
                        (d.Status != null && d.Status.Contains(search)) ||
                        (d.CurrentOffice != null && d.CurrentOffice.Name != null && d.CurrentOffice.Name.Contains(search)));

                var total = await query.CountAsync();
                var items = await query
                    .OrderByDescending(d => d.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return (items, total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetOfficeHistoryAsync] Failed for officeId {OfficeId}", officeId);
                return (new List<Document>(), 0);
            }
        }

        public async Task<(List<Document> Items, int TotalCount)> GetUserDocumentsAsync(
            string email, int page, int pageSize,
            string? search = null, string? status = null,
            string? priority = null, string? type = null)
        {
            try
            {
                var query = _context.Documents
                    .Include(d => d.Creator)
                    .Include(d => d.NextOffice)
                    .Include(d => d.NextUnit)
                    .Include(d => d.CurrentOffice)
                    .Include(d => d.CurrentUnit)
                    .Where(d => d.Creator != null && d.Creator.Email == email);

                if (!string.IsNullOrEmpty(search))
                    query = query.Where(d =>
                        (d.Name != null && d.Name.Contains(search)) ||
                        (d.Type != null && d.Type.Contains(search)) ||
                        (d.Status != null && d.Status.Contains(search)) ||
                        (d.Priority != null && d.Priority.Contains(search)) ||
                        (d.CurrentOffice != null && d.CurrentOffice.Name != null && d.CurrentOffice.Name.Contains(search)) ||
                        (d.NextOffice != null && d.NextOffice.Name != null && d.NextOffice.Name.Contains(search)));

                if (!string.IsNullOrEmpty(status))
                    query = query.Where(d => d.Status == status);

                if (!string.IsNullOrEmpty(priority))
                    query = query.Where(d => d.Priority == priority);

                if (!string.IsNullOrEmpty(type))
                    query = query.Where(d => d.Type == type);

                var total = await query.CountAsync();
                var items = await query
                    .OrderByDescending(d => d.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return (items, total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetUserDocumentsAsync] Failed for email {Email}", email);
                return (new List<Document>(), 0);
            }
        }

        public async Task<(List<DocumentLog> Items, int TotalCount)> GetAuditLogsAsync(
            int page, int pageSize,
            string? search = null, string? action = null, string? date = null)
        {
            try
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

                return (items, total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetAuditLogsAsync] Failed");
                return (new List<DocumentLog>(), 0);
            }
        }

        public IAsyncEnumerable<Document> StreamAllDocumentsAsync(
            string? search = null, string? status = null, string? office = null,
            string? dateFrom = null, string? dateTo = null)
        {
            var query = _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.CurrentOffice)
                .Include(d => d.CurrentUnit)
                .Include(d => d.NextOffice)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(d =>
                    (d.Name != null && d.Name.Contains(search)) ||
                    (d.ReferenceNumber != null && d.ReferenceNumber.Contains(search)));

            if (!string.IsNullOrEmpty(status))
                query = query.Where(d => d.Status == status);

            if (!string.IsNullOrEmpty(office))
                query = query.Where(d =>
                    (d.CurrentOffice != null && d.CurrentOffice.Name == office) ||
                    (d.NextOffice != null && d.NextOffice.Name == office));

            if (DateTime.TryParse(dateFrom, out var from))
                query = query.Where(d => d.CreatedAt >= from);

            if (DateTime.TryParse(dateTo, out var to))
                query = query.Where(d => d.CreatedAt <= to.AddDays(1));

            return query
                .OrderByDescending(d => d.CreatedAt)
                .AsAsyncEnumerable();
        }

        public IAsyncEnumerable<DocumentLog> StreamAllAuditLogsAsync(
            string? search = null, string? action = null, string? date = null)
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
                    (m.AppUser != null && m.AppUser.Name != null && m.AppUser.Name.Contains(search)) ||
                    (m.Action != null && m.Action.Contains(search)));

            if (!string.IsNullOrEmpty(action))
                query = query.Where(m => m.Action == action);

            if (DateTime.TryParse(date, out var filterDate))
                query = query.Where(m => m.TimeStamp.Date == filterDate.Date);

            return query
                .OrderByDescending(m => m.TimeStamp)
                .AsAsyncEnumerable();
        }
    }

    public class DashboardStats
    {
        public int TotalDocuments { get; set; }
        public int TotalOffices { get; set; }
        public int TotalUsers { get; set; }
        public int InMotionCount { get; set; }
        public int ReceivedCount { get; set; }
        public int CompletedCount { get; set; }
        public List<OfficeWorkload> OfficeWorkloads { get; set; } = new();
        public List<Document> RecentDocuments { get; set; } = new();
        public List<Document> InMotionDocs { get; set; } = new();
    }

    public class OfficeWorkload
    {
        public int OfficeId { get; set; }
        public string? OfficeName { get; set; }
        public int DocumentCount { get; set; }
        public int Percentage { get; set; }
    }
}
