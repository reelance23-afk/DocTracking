using DocTracking.Client.Models;
using DocTracking.Data;
using Microsoft.EntityFrameworkCore;

namespace DocTracking.Services
{
    public class DocumentQueryService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DocumentQueryService> _logger;

        private IQueryable<Document> ApplyDocumentFilters(
        IQueryable<Document> query,
        string? search, string? status, string? office,
        string? dateFrom, string? dateTo, string? sender = null)
        {
            if (!string.IsNullOrEmpty(search))
                query = query.Where(d =>
                    (d.Name != null && d.Name.Contains(search)) ||
                    (d.ReferenceNumber != null && d.ReferenceNumber.Contains(search)));
            if (!string.IsNullOrEmpty(sender))
                query = query.Where(d => d.Creator != null && d.Creator.Name != null && d.Creator.Name.Contains(sender));
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
            return query;
        }

        private IQueryable<DocumentLog> ApplyAuditFilters(
            IQueryable<DocumentLog> query,
            string? search, string? action, string? date,
            string? sender = null, string? office = null)
        {
            if (!string.IsNullOrEmpty(search))
                query = query.Where(m =>
                    m.Document != null && m.Document.Name != null && m.Document.Name.Contains(search));
            if (!string.IsNullOrEmpty(action))
                query = query.Where(m => m.Action == action);
            if (DateTime.TryParse(date, out var filterDate))
                query = query.Where(m => m.TimeStamp.Date == filterDate.Date);
            if (!string.IsNullOrEmpty(sender))
                query = query.Where(m => m.AppUser != null && m.AppUser.Name != null && m.AppUser.Name.Contains(sender));
            if (!string.IsNullOrEmpty(office))
                query = query.Where(m =>
                    (m.Office != null && m.Office.Name != null && m.Office.Name.Contains(office)) ||
                    (m.OfficeName != null && m.OfficeName.Contains(office)));
            return query;
        }

        private static IOrderedQueryable<Document> OrderByPriority(IQueryable<Document> query) =>
        query.OrderBy(d => d.Priority == "Emergency" ? 0 :
                       d.Priority == "Urgent" ? 1 :
                       d.Priority == "Medium" ? 2 :
                       d.Priority == "Low" ? 3 : 99);

        public DocumentQueryService(ApplicationDbContext context, ILogger<DocumentQueryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<(List<Document> Items, int TotalCount)> GetAllDocumentsAsync(
            int page, int pageSize,
            string? search = null, string? status = null,
            string? office = null, string? dateFrom = null, string? dateTo = null,
            string? sender = null)
        {
            try
            {
                var query = ApplyDocumentFilters(
                    _context.Documents.Include(d => d.Creator).Include(d => d.NextOffice)
                        .Include(d => d.CurrentOffice).Include(d => d.NextUnit).Include(d => d.CurrentUnit),
                    search, status, office, dateFrom, dateTo, sender);

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


        public async Task<AdminDashboardStats> GetDashboardStatsAsync()
        {
            try
            {
                var docStats = await _context.Documents
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Total = g.Count(),
                        InMotion = g.Count(d => d.Status == "In Motion"),
                        Received = g.Count(d => d.Status == "Received"),
                        Completed = g.Count(d => d.Status == "Completed")
                    })
                    .FirstOrDefaultAsync();

                var totalOffices = await _context.Offices.CountAsync();
                var totalUsers = await _context.AppUsers.CountAsync();

                var officeWorkloads = await _context.Documents
                    .Where(d => d.CurrentOfficeId != null)
                    .GroupBy(d => new { d.CurrentOfficeId, d.CurrentOffice!.Name })
                    .Select(g => new OfficeWorkload
                    {
                        OfficeId = g.Key.CurrentOfficeId!.Value,
                        OfficeName = g.Key.Name,
                        DocumentCount = g.Count()
                    })
                    .OrderByDescending(w => w.DocumentCount)
                    .ToListAsync();

                    var unitWorkloads = await _context.Documents
                    .Where(d => d.CurrentUnitId != null)
                    .GroupBy(d => new { d.CurrentUnitId, d.CurrentUnit!.Name, d.CurrentUnit.OfficeId })
                    .Select(g => new UnitWorkload
                    {
                       UnitId = g.Key.CurrentUnitId!.Value,
                       UnitName = g.Key.Name,
                       DocumentCount = g.Count(),
                       OfficeId = g.Key.OfficeId
                    })
                    .ToListAsync();

                foreach (var w in officeWorkloads)
                    w.UnitWorkloads = unitWorkloads.Where(u => u.OfficeId == w.OfficeId).ToList();

                var recentDocuments = await _context.Documents
                    .Include(d => d.Creator)
                    .OrderByDescending(d => d.LastActionDate ?? d.CreatedAt)
                    .Take(3)
                    .ToListAsync();

                var inMotionDocs = await _context.Documents
                    .Include(d => d.Creator)
                    .Where(d => d.Status == "In Motion")
                    .OrderByDescending(d => d.LastActionDate ?? d.CreatedAt)
                    .Take(4)
                    .ToListAsync();

                int maxDoc = Math.Max(docStats?.Total ?? 1, 1);
                foreach (var w in officeWorkloads)
                    w.Percentage = (int)Math.Round((double)w.DocumentCount / maxDoc * 100);

                return new AdminDashboardStats
                {
                    TotalDocuments = docStats?.Total ?? 0,
                    TotalOffices = totalOffices,
                    TotalUsers = totalUsers,
                    InMotionCount = docStats?.InMotion ?? 0,
                    ReceivedCount = docStats?.Received ?? 0,
                    CompletedCount = docStats?.Completed ?? 0,
                    OfficeWorkloads = officeWorkloads,
                    RecentDocuments = recentDocuments,
                    InMotionDocs = inMotionDocs
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetDashboardStatsAsync] Failed");
                return new AdminDashboardStats();
            }
        }

        public IAsyncEnumerable<Document> StreamAllDocumentsAsync(
            string? search = null, string? status = null, string? office = null,
            string? dateFrom = null, string? dateTo = null, string? sender = null)
        {
            return ApplyDocumentFilters(
                _context.Documents
                .Include(d => d.Creator)
                .Include(d => d.CurrentOffice)
                .Include(d => d.CurrentUnit)
                .Include(d => d.NextOffice),
                search, status, office, dateFrom, dateTo, sender)
                .OrderByDescending(d => d.CreatedAt)
                .AsAsyncEnumerable();
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

                var docIds = items.Select(d => d.Id).ToList();
                var receivedDocIds = await _context.DocumentLogs
                    .Where(l => docIds.Contains(l.DocumentId) && l.Action == "Received")
                    .Select(l => l.DocumentId)
                    .Distinct()
                    .ToListAsync();

                foreach (var doc in items)
                    doc.HasBeenReceived = receivedDocIds.Contains(doc.Id);

                return (items, total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetUserDocumentsAsync] Failed for email {Email}", email);
                return (new List<Document>(), 0);
            }
        }


        public async Task<UserDocumentStats> GetUserDocumentStatsAsync(string? email)
        {
            try
            {
                var stats = await _context.Documents
                    .Where(g => g.Creator != null && g.Creator.Email == email)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        TotalInMotion = g.Count(g => g.Status == "In Motion"),
                        TotalReceived = g.Count(g => g.Status == "Received"),
                        TotalCompleted = g.Count(g => g.Status == "Completed"),
                        Total = g.Count()
                    }) 
                    .FirstOrDefaultAsync();

                return new UserDocumentStats
                {
                    TotalInMotionCount = stats?.TotalInMotion ?? 0,
                    TotalReceivedCount = stats?.TotalReceived ?? 0,
                    TotalCompletedCount = stats?.TotalCompleted ?? 0,
                    TotalCount = stats?.Total ?? 0
                };

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetUserDocumentStatsAsync] Failed");
                return new UserDocumentStats();
            }
        }

        public async Task<UserHomeData> GetUserHomeDataAsync(string? email)
        {
            try
            {
                var baseQuery = _context.Documents.Where(d => d.Creator != null && d.Creator.Email == email);

                var stats = await baseQuery
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Total = g.Count(),
                        InMotion = g.Count(d => d.Status == "In Motion" || d.Status == "Received"),
                        Completed = g.Count(d => d.Status == "Completed")
                    })
                    .FirstOrDefaultAsync();

                var allRecent = await baseQuery
                    .Include(d => d.Creator).Include(d => d.NextOffice).Include(d => d.CurrentOffice)
                    .OrderByDescending(d => d.LastActionDate ?? d.CreatedAt)
                    .Take(500)
                    .ToListAsync();

                var stuckThreshold = DateTime.UtcNow.AddDays(-1);

                return new UserHomeData
                {
                    TotalDocumentsCount = stats?.Total ?? 0,
                    TotalInProgressCount = stats?.InMotion ?? 0,
                    TotalCompletedCount = stats?.Completed ?? 0,
                    InProgressDoc = allRecent.Where(d => d.Status == "In Motion" || d.Status == "Received").Take(5).ToList(),
                    CompletedDoc = allRecent.Where(d => d.Status == "Completed").Take(5).ToList(),
                    RecentDocuments = allRecent,
                    StuckDocuments = allRecent.Where(d => d.Status == "In Motion" && (d.LastActionDate ?? d.CreatedAt) < stuckThreshold).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetUserHomeDataAsync] Failed for email {Email}", email);
                return new UserHomeData();
            }
        }


        public async Task<List<Document>> GetUserActivityAsync(int userId)
        {
            try
            {
                return await _context.Documents
                    .Include(d => d.Creator)
                    .Include(d => d.CurrentOffice)
                    .Include(d => d.CurrentUnit)
                    .Include(d => d.NextOffice)
                    .Include(d => d.NextUnit)
                    .Where(d => d.CreatorId == userId ||
                        _context.DocumentLogs
                        .Any(l => l.AppUserId == userId && l.DocumentId == d.Id))
                    .OrderByDescending(d => d.LastActionDate ?? d.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetUserActivityAsync] Failed for userId {UserId}", userId);
                return new List<Document>();
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

                return await OrderByPriority(query).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetIncomingAsync] Failed for officeId {OfficeId}", officeId);
                return new List<Document>();
            }
        }

        public async Task<List<Document>> GetDeskDocumentsAsync(
            int officeId, int? unitId,bool isOfficeHead = false, string? search = null)
        {
            try
            {
                var query = _context.Documents
                    .Include(d => d.Creator)
                    .Include(d => d.CurrentOffice)
                    .Include(d => d.CurrentUnit)
                    .Where(d => d.CurrentOfficeId == officeId && d.Status == "Received");

                if(!isOfficeHead)
                {
                if (unitId.HasValue)
                    query = query.Where(d => d.CurrentUnitId == null || d.CurrentUnitId == unitId);
                else
                    query = query.Where(d => d.CurrentUnitId == null);
                }

                if (!string.IsNullOrEmpty(search))
                    query = query.Where(d =>
                        (d.Name != null && d.Name.Contains(search)) ||
                        (d.Type != null && d.Type.Contains(search)));

                return await OrderByPriority(query).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetDeskDocumentsAsync] Failed for officeId {OfficeId}", officeId);
                return new List<Document>();
            }
        }

        public async Task<List<Document>> GetOutgoingAsync(int? unitId, int? officeId, string? search = null)
        {
            try
            {
                var query = _context.Documents
                    .Include(d => d.Creator)
                    .Include(d => d.NextOffice)
                    .Include(d => d.NextUnit)
                    .Where(d => d.Status == "In Motion" &&
                        _context.DocumentLogs
                    .Any(log => log.Action == "Forwarded" &&
                            log.DocumentId == d.Id && log.AppUser != null &&
                            (unitId.HasValue
                                ? log.AppUser.UnitId == unitId
                                : (log.AppUser.Unit != null && log.AppUser.Unit.OfficeId == officeId) ||
                                  (log.AppUser.Unit == null && log.AppUser.OfficeId == officeId))));

                if (!string.IsNullOrEmpty(search))
                    query = query.Where(d =>
                        (d.Name != null && d.Name.Contains(search)) ||
                        (d.Type != null && d.Type.Contains(search)) ||
                        (d.NextOffice != null && d.NextOffice.Name != null && d.NextOffice.Name.Contains(search)));

                return await OrderByPriority(query)
                    .ThenByDescending(d => d.LastActionDate ?? d.CreatedAt)
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
                var query = _context.Documents
                    .Include(d => d.Creator)
                    .Include(d => d.NextOffice)
                    .Include(d => d.CurrentOffice)
                    .Include(d => d.NextUnit)
                    .Include(d => d.CurrentUnit)
                    .Where(d => _context.DocumentLogs
                    .Any(l => l.UnitId == unitId && l.DocumentId == d.Id));

                if (!string.IsNullOrEmpty(search))
                    query = query.Where(d =>
                        (d.Name != null && d.Name.Contains(search)) ||
                        (d.Type != null && d.Type.Contains(search)) ||
                        (d.Status != null && d.Status.Contains(search)) ||
                        (d.CurrentOffice != null && d.CurrentOffice.Name != null && d.CurrentOffice.Name.Contains(search)));

                var total = await query.CountAsync();
                var items = await query.OrderByDescending(d => d.CreatedAt)
                    .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
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
                var query = _context.Documents
                    .Include(d => d.Creator)
                    .Include(d => d.NextOffice)
                    .Include(d => d.CurrentOffice)
                    .Include(d => d.NextUnit)
                    .Include(d => d.CurrentUnit)
                    .Where(d => _context.DocumentLogs
                    .Any(l => l.DocumentId == d.Id &&
                        ((l.UnitId != null && l.Unit != null && l.Unit.OfficeId == officeId) ||
                         (l.UnitId == null && l.OfficeId == officeId))));

                if (!string.IsNullOrEmpty(search))
                    query = query.Where(d =>
                        (d.Name != null && d.Name.Contains(search)) ||
                        (d.Type != null && d.Type.Contains(search)) ||
                        (d.Status != null && d.Status.Contains(search)) ||
                        (d.CurrentOffice != null && d.CurrentOffice.Name != null && d.CurrentOffice.Name.Contains(search)));

                var total = await query.CountAsync();
                var items = await query.OrderByDescending(d => d.CreatedAt)
                    .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                return (items, total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetOfficeHistoryAsync] Failed for officeId {OfficeId}", officeId);
                return (new List<Document>(), 0);
            }
        }

        public async Task<LocationDocStats> GetLocationDocStatAsync(int? unitId, int? officeId)
        {
            try
            {
                IQueryable<Document> query;

                if (unitId.HasValue)
                {
                    query = _context.Documents
                        .Where(d => _context.DocumentLogs
                        .Any(i => i.DocumentId == d.Id && i.UnitId == unitId));
                }
                else if (officeId.HasValue)
                {
                    query = _context.Documents
                        .Where(d => _context.DocumentLogs
                        .Any(i => i.DocumentId == d.Id &&
                        ((i.UnitId != null && i.Unit != null && i.Unit.OfficeId == officeId) ||
                        (i.UnitId == null && i.OfficeId == officeId))));
                }
                else
                    return new LocationDocStats();

                var stats = await query
                    .GroupBy(_ => 1)
                    .Select(d => new LocationDocStats
                    {
                        InMotion = d.Count(d => d.Status == "In Motion"),
                        Received = d.Count(d => d.Status == "Received"),
                        Completed = d.Count(d => d.Status == "Completed")
                    })
                    .FirstOrDefaultAsync();

                return stats ?? new LocationDocStats();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GetLocationDocStatsAsync] Failed");
                return new LocationDocStats();
            }
        }
        public async Task<(List<DocumentLog> Items, int TotalCount)> GetAuditLogsAsync(
            int page, int pageSize,
            string? search = null, string? action = null, string? date = null,
            string? sender = null, string? office = null)
        {
            try
            {
                var query = _context.DocumentLogs
                    .Include(m => m.Document)
                    .Include(m => m.Office)
                    .Include(m => m.Unit)
                    .Include(m => m.AppUser)
                    .AsQueryable();

                query = ApplyAuditFilters(query, search, action, date, sender, office);

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
        public IAsyncEnumerable<DocumentLog> StreamAllAuditLogsAsync(
            string? search = null, string? action = null, string? date = null,
            string? sender = null, string? office = null)
        {
            var query = _context.DocumentLogs
                .Include(m => m.Document)
                .Include(m => m.Office)
                .Include(m => m.Unit)
                .Include(m => m.AppUser)
                .AsQueryable();

            query = ApplyAuditFilters(query, search, action, date, sender, office);

            return query
                .OrderByDescending(m => m.TimeStamp)
                .AsAsyncEnumerable();
        }
    }
    public class AdminDashboardStats
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
        public List<UnitWorkload> UnitWorkloads { get; set; } = new();
    }

    public class UnitWorkload
    {
        public int UnitId { get; set; }
        public string? UnitName { get; set; }
        public int DocumentCount { get; set; }
        public int Percentage { get; set; }
        public int OfficeId { get; set; }
    }

    public class UserHomeData
    {
        public int TotalInProgressCount { get; set; }
        public int TotalCompletedCount { get; set; }
        public int TotalDocumentsCount { get; set; }
        public List<Document> InProgressDoc { get; set; } = new();
        public List<Document> CompletedDoc { get; set; } = new();
        public List<Document> RecentDocuments { get; set; } = new();
        public List<Document> StuckDocuments { get; set; } = new();
    }

    public class UserDocumentStats
    {
        public int TotalInMotionCount { get; set; }
        public int TotalReceivedCount { get; set; }
        public int TotalCompletedCount { get; set; }
        public int TotalCount { get; set; }
    }

    public class LocationDocStats
    {
        public int InMotion { get; set; }
        public int Received { get; set; }
        public int Completed { get; set; }
    }
}
