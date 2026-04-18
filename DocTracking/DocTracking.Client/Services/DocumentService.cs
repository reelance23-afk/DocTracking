using DocTracking.Client.Models;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Json;
using System.Text.Json;

namespace DocTracking.Client.Services
{
    public class DocumentService
    {
        private readonly HttpClient _http;
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public DocumentService(HttpClient http)
        {
            _http = http;
        }

        private async Task<T?> GetJsonAsync<T>(string url)
        {
            try
            {
                var response = await _http.GetAsync(url);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new UnauthorizedAccessException();
                if (!response.IsSuccessStatusCode) return default;
                var content = await response.Content.ReadAsStringAsync();
                if (content.TrimStart().StartsWith('<')) return default;
                return JsonSerializer.Deserialize<T>(content, _jsonOptions);
            }
            catch (UnauthorizedAccessException) { throw; }
            catch (Exception ex)
            {
                Console.WriteLine($"[DocumentService] GetJsonAsync failed for {url}: {ex.Message}");
                return default;
            }
        }

        private async Task<(bool Success, string? Error)> ToResult(Task<HttpResponseMessage> task)
        {
            try
            {
                var response = await task;
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new UnauthorizedAccessException();
                if (response.IsSuccessStatusCode) return (true, null);
                return (false, await response.Content.ReadAsStringAsync());
            }
            catch (UnauthorizedAccessException) { throw; }
            catch (Exception ex)
            {
                Console.WriteLine($"[DocumentService] ToResult failed: {ex.Message}");
                return (false, "An unexpected error occurred.");
            }
        }

        public async Task<List<Office>> GetOfficesAsync() =>
            (await GetJsonAsync<List<Office>>("api/offices/simple")) ?? new();

        public async Task<Office?> GetOfficeByIdAsync(int id) =>
            await GetJsonAsync<Office>($"api/offices/{id}");

        public async Task<List<Office>> GetOfficesSimpleAsync(string? search = null)
        {
            var url = "api/offices/simple";
            if (!string.IsNullOrEmpty(search)) url += $"?search={Uri.EscapeDataString(search)}";
            return await GetJsonAsync<List<Office>>(url) ?? new();
        }

        public async Task<List<Unit>> GetUnitsByOfficeAsync(int officeId) =>
            await GetJsonAsync<List<Unit>>($"api/units?officeId={officeId}") ?? new();


        public async Task<PagedResult<Office>> GetOfficesPagedAsync(
            int page = 1, int pageSize = 25, string? search = null)
        {
            var url = $"api/offices?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
            return await GetJsonAsync<PagedResult<Office>>(url) ?? new();
        }

        public async Task<(bool Success, string? Error, Office? Created)> AddOfficeAsync(Office office)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("api/offices", office);
                if (!response.IsSuccessStatusCode)
                    return (false, await response.Content.ReadAsStringAsync(), null);
                var created = await response.Content.ReadFromJsonAsync<Office>(_jsonOptions);
                return (true, null, created);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DocumentService] AddOfficeAsync failed: {ex.Message}");
                return (false, "An unexpected error occurred.", null);
            }
        }


        public Task<(bool Success, string? Error)> UpdateOfficeAsync(Office office) =>
            ToResult(_http.PutAsJsonAsync($"api/offices/{office.Id}", office));

        public Task<(bool Success, string? Error)> DeleteOfficeAsync(int id) =>
            ToResult(_http.DeleteAsync($"api/offices/{id}"));

        public async Task<List<Unit>> GetUnitsAsync() =>
            await GetJsonAsync<List<Unit>>("api/units") ?? new();

        public Task<(bool Success, string? Error)> AddUnitAsync(Unit unit) =>
            ToResult(_http.PostAsJsonAsync("api/units", unit));

        public Task<(bool Success, string? Error)> UpdateUnitAsync(Unit unit) =>
            ToResult(_http.PutAsJsonAsync($"api/units/{unit.Id}", unit));

        public Task<(bool Success, string? Error)> DeleteUnitAsync(int id) =>
            ToResult(_http.DeleteAsync($"api/units/{id}"));

        public async Task<List<AppUser>> GetAppUserAsync()
        {
            var result = await GetJsonAsync<PagedResult<AppUser>>("api/appusers?pageSize=1000");
            return result?.Items ?? new();
        }

        public async Task<PagedResult<AppUser>> GetAppUsersPagedAsync(
            int page = 1, int pageSize = 25, string? search = null,
            int? officeId = null, int? unitId = null, bool filterByOffice = false)
        {
            var url = $"api/appusers?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
            if (officeId.HasValue) url += $"&officeId={officeId.Value}";
            if (unitId.HasValue) url += $"&unitId={unitId.Value}";
            if (filterByOffice) url += $"&filterByOffice=true";
            return await GetJsonAsync<PagedResult<AppUser>>(url) ?? new();
        }


        public async Task<AppUser?> GetProfileAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/documents/my-profile");
                if (!response.IsSuccessStatusCode) return null;
                var content = await response.Content.ReadAsStringAsync();
                if (content.TrimStart().StartsWith('<')) return null;
                return JsonSerializer.Deserialize<AppUser>(content, _jsonOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DocumentService] GetProfileAsync failed: {ex.Message}");
                return null;
            }
        }

        public Task<(bool Success, string? Error)> UpdateAppUserAsync(AppUser user) =>
            ToResult(_http.PutAsJsonAsync($"api/appusers/{user.Id}", user));

        public Task<(bool Success, string? Error)> DeleteAppUserAsync(int id) =>
            ToResult(_http.DeleteAsync($"api/appusers/{id}"));

        public Task<(bool Success, string? Error)> BulkReassignUsersAsync(int fromUnitId, int toUnitId) =>
            ToResult(_http.PutAsJsonAsync("api/appusers/bulk-reassign", new { FromUnitId = fromUnitId, ToUnitId = toUnitId }));

        public Task<(bool Success, string? Error)> SelectiveReassignUsersAsync(List<int> userIds, int toUnitId) =>
            ToResult(_http.PutAsJsonAsync("api/appusers/selective-reassign", new { UserIds = userIds, ToUnitId = toUnitId }));

        public async Task<List<Document>> GetUserActivityAsync(int userId) =>
            await GetJsonAsync<List<Document>>($"api/documents/activity/user/{userId}") ?? new();

        public async Task<PagedResult<Document>> GetAllDocumentsAsync(
            int page = 1, int pageSize = 25,
            string? search = null, string? status = null,
            string? office = null, string? dateFrom = null, string? dateTo = null,
            string? sender = null)
        {
            var url = $"api/documents?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
            if (!string.IsNullOrEmpty(status)) url += $"&status={Uri.EscapeDataString(status)}";
            if (!string.IsNullOrEmpty(office)) url += $"&office={Uri.EscapeDataString(office)}";
            if (!string.IsNullOrEmpty(dateFrom)) url += $"&dateFrom={Uri.EscapeDataString(dateFrom)}";
            if (!string.IsNullOrEmpty(dateTo)) url += $"&dateTo={Uri.EscapeDataString(dateTo)}";
            if (!string.IsNullOrEmpty(sender)) url += $"&sender={Uri.EscapeDataString(sender)}";
            return await GetJsonAsync<PagedResult<Document>>(url) ?? new();
        }

        public string GetDocumentsCsvUrl(
            string? search = null, string? status = null,
            string? office = null, string? dateFrom = null, string? dateTo = null,
            string? sender = null)
        {
            var url = $"{_http.BaseAddress}api/documents/export-csv";
            var query = new List<string>();
            if (!string.IsNullOrEmpty(search)) query.Add($"search={Uri.EscapeDataString(search)}");
            if (!string.IsNullOrEmpty(status)) query.Add($"status={Uri.EscapeDataString(status)}");
            if (!string.IsNullOrEmpty(office)) query.Add($"office={Uri.EscapeDataString(office)}");
            if (!string.IsNullOrEmpty(dateFrom)) query.Add($"dateFrom={Uri.EscapeDataString(dateFrom)}");
            if (!string.IsNullOrEmpty(dateTo)) query.Add($"dateTo={Uri.EscapeDataString(dateTo)}");
            if (!string.IsNullOrEmpty(sender)) query.Add($"sender={Uri.EscapeDataString(sender)}");
            if (query.Any()) url += "?" + string.Join("&", query);
            return url;
        }


        public async Task<DocumentContext?> GetDocumentContextAsync(string referenceNumber) =>
            await GetJsonAsync<DocumentContext>($"api/documents/by-ref/{referenceNumber}/context");

        public async Task<AdminDashboardStats?> GetDashboardStatsAsync() =>
            await GetJsonAsync<AdminDashboardStats>("api/documents/dashboard-stats");

        public async Task<PagedResult<Document>> GetUserDocumentsAsync(
            string email,
            int page = 1, int pageSize = 25,
            string? search = null,
            string? status = null,
            string? priority = null,
            string? type = null)
        {
            var url = $"api/documents/user/{email}?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
            if (!string.IsNullOrEmpty(status)) url += $"&status={Uri.EscapeDataString(status)}";
            if (!string.IsNullOrEmpty(priority)) url += $"&priority={Uri.EscapeDataString(priority)}";
            if (!string.IsNullOrEmpty(type)) url += $"&type={Uri.EscapeDataString(type)}";
            return await GetJsonAsync<PagedResult<Document>>(url) ?? new();
        }

        public async Task<UserDocumentStats?> GetUserDocumentStatsAsync(string email) =>
            await GetJsonAsync<UserDocumentStats>($"api/documents/user/{email}/stats");

        public async Task<UserHomeData> GetUserHomeDataAsync(string email) =>
            await GetJsonAsync<UserHomeData>($"api/documents/user/{Uri.EscapeDataString(email)}/home-data");

        public async Task<LocationDocStats?> GetLocationDocStatsAsync(int? unitId, int? officeId)
        {
            var url = unitId.HasValue
                ? $"api/documents/stats/unit/{unitId}"
                : $"api/documents/stats/office/{officeId}";
            return await GetJsonAsync<LocationDocStats>(url);
        }

        public async Task<List<Document>> GetIncomingAsync(int officeId, int? unitId = null, string? search = null)
        {
            var url = $"api/documents/incoming/{officeId}";
            var query = new List<string>();
            if (unitId.HasValue) query.Add($"unitId={unitId.Value}");
            if (!string.IsNullOrEmpty(search)) query.Add($"search={Uri.EscapeDataString(search)}");
            if (query.Any()) url += "?" + string.Join("&", query);
            return await GetJsonAsync<List<Document>>(url) ?? new();
        }

        public async Task<List<Document>> GetDeskDocumentsAsync(int officeId, int? unitId = null, string? search = null)
        {
            var url = $"api/documents/desk/{officeId}";
            var query = new List<string>();
            if (unitId.HasValue) query.Add($"unitId={unitId.Value}");
            if (!string.IsNullOrEmpty(search)) query.Add($"search={Uri.EscapeDataString(search)}");
            if (query.Any()) url += "?" + string.Join("&", query);
            return await GetJsonAsync<List<Document>>(url) ?? new();
        }

        public async Task<List<Document>> GetOutgoingAsync(string? search = null)
        {
            var url = "api/documents/outgoing/user";
            if (!string.IsNullOrEmpty(search)) url += $"?search={Uri.EscapeDataString(search)}";
            return await GetJsonAsync<List<Document>>(url) ?? new();
        }

        public async Task<PagedResult<Document>> GetUnitHistoryAsync(
            int unitId, int page = 1, int pageSize = 25, string? search = null)
        {
            var url = $"api/documents/history/unit/{unitId}?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
            return await GetJsonAsync<PagedResult<Document>>(url) ?? new();
        }

        public async Task<PagedResult<Document>> GetOfficeHistoryAsync(
            int officeId, int page = 1, int pageSize = 25, string? search = null)
        {
            var url = $"api/documents/history/office/{officeId}?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
            return await GetJsonAsync<PagedResult<Document>>(url) ?? new();
        }

        public Task<(bool Success, string? Error)> CreateDocumentAsync(Document doc) =>
            ToResult(_http.PostAsJsonAsync("api/documents", doc));

        public async Task<string?> UploadFileAsync(IBrowserFile file)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var fileContent = new StreamContent(file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024));
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                content.Add(fileContent, "file", file.Name);
                var response = await _http.PostAsync("api/documents/upload", content);
                if (!response.IsSuccessStatusCode) return null;
                var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
                return result?.FilePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DocumentService] UploadFileAsync failed: {ex.Message}");
                return null;
            }
        }

        public async Task DeleteUploadAsync(string path)
        {
            try
            {
                await _http.DeleteAsync($"api/documents/upload?path={Uri.EscapeDataString(path)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DocumentService] DeleteUploadAsync failed: {ex.Message}");
            }
        }

        public Task<(bool Success, string? Error)> ReceivedDocumentAsync(int id) =>
            ToResult(_http.PutAsync($"api/documents/{id}/receive", null));

        public Task<(bool Success, string? Error)> ForwardDocumentAsync(int id, int nextOfficeId, int? nextUnitId = null, string? comment = null) =>
            ToResult(_http.PutAsJsonAsync($"api/documents/{id}/forward", new { NextOfficeId = nextOfficeId, NextUnitId = nextUnitId, Comment = comment }));

        public Task<(bool Success, string? Error)> FinishDocumentAsync(int id, string? comment = null) =>
            ToResult(_http.PutAsJsonAsync($"api/documents/{id}/finish", new { Comment = comment }));

        public Task<(bool Success, string? Error)> UpdateDocumentAsync(Document doc) =>
            ToResult(_http.PutAsJsonAsync($"api/documents/{doc.Id}", doc));

        public Task<(bool Success, string? Error)> DeleteDocumentAsync(int id) =>
            ToResult(_http.DeleteAsync($"api/documents/{id}"));

        public Task<(bool Success, string? Error)> AdminOverrideDocumentAsync(int id, AdminOverridePayload request) =>
            ToResult(_http.PutAsJsonAsync($"api/documents/{id}/admin-override", request));

        public Task<(bool Success, string? Error)> BulkDeleteDocumentsAsync(List<int> ids) =>
            ToResult(_http.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "api/documents/bulk")
            {
                Content = JsonContent.Create(ids)
            }));

        public string GetQRCodeUrl(int documentId) => $"{_http.BaseAddress}api/documents/{documentId}/qrcode";

        public async Task<Document?> GetDocumentByRefAsync(string referenceNumber)
        {
            try
            {
                var response = await _http.GetAsync($"api/documents/by-ref/{referenceNumber}");
                if (!response.IsSuccessStatusCode) return null;
                var content = await response.Content.ReadAsStringAsync();
                if (content.TrimStart().StartsWith('<')) return null;
                return JsonSerializer.Deserialize<Document>(content, _jsonOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DocumentService] GetDocumentByRefAsync failed: {ex.Message}");
                return null;
            }
        }

        public async Task<List<DocumentLog>> GetDocumentLogsAsync(int id) =>
            await GetJsonAsync<List<DocumentLog>>($"api/documentlogs/{id}") ?? new();

        public async Task<PagedResult<DocumentLog>> GetAuditLogsAsync(
            int page = 1, int pageSize = 25,
            string? search = null, string? action = null, string? date = null,
            string? sender = null, string? office = null)
        {
            var url = $"api/documentlogs/audit?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
            if (!string.IsNullOrEmpty(action)) url += $"&action={Uri.EscapeDataString(action)}";
            if (!string.IsNullOrEmpty(date)) url += $"&date={Uri.EscapeDataString(date)}";
            if (!string.IsNullOrEmpty(sender)) url += $"&sender={Uri.EscapeDataString(sender)}";
            if (!string.IsNullOrEmpty(office)) url += $"&office={Uri.EscapeDataString(office)}";
            return await GetJsonAsync<PagedResult<DocumentLog>>(url) ?? new();
        }


        public Task<(bool Success, string? Error)> BroadcastNotificationAsync(string message, int? officeId = null) =>
            ToResult(_http.PostAsJsonAsync("api/notifications/broadcast", new { Message = message, OfficeId = officeId }));

        public string GetAuditLogCsvUrl(
            string? search = null, string? action = null, string? date = null,
            string? sender = null, string? office = null)
        {
            var url = $"{_http.BaseAddress}api/documentlogs/export-csv";
            var query = new List<string>();
            if (!string.IsNullOrEmpty(search)) query.Add($"search={Uri.EscapeDataString(search)}");
            if (!string.IsNullOrEmpty(action)) query.Add($"action={Uri.EscapeDataString(action)}");
            if (!string.IsNullOrEmpty(date)) query.Add($"date={Uri.EscapeDataString(date)}");
            if (!string.IsNullOrEmpty(sender)) query.Add($"sender={Uri.EscapeDataString(sender)}");
            if (!string.IsNullOrEmpty(office)) query.Add($"office={Uri.EscapeDataString(office)}");
            if (query.Any()) url += "?" + string.Join("&", query);
            return url;
        }

        public async Task<GroupedSearchResult> SearchDocumentsGroupedAsync(string q, int take = 5)
        {
            return await GetJsonAsync<GroupedSearchResult>(
                $"api/documents/search/grouped?q={Uri.EscapeDataString(q)}&take={take}") ?? new();
        }

        public async Task<List<string>> GetSendersAsync(string? search = null)
        {
            var url = "api/documents/senders";
            if (!string.IsNullOrEmpty(search)) url += $"?search={Uri.EscapeDataString(search)}";
            return await GetJsonAsync<List<string>>(url) ?? new();
        }
    }

    public class UploadResponse
    {
        public string? FilePath { get; set; }
    }

    public class AdminOverridePayload
    {
        public string? Status { get; set; }
        public string? ForceComment { get; set; }
        public int? NextOfficeId { get; set; }
        public int? NextUnitId { get; set; }
        public string? ReassignComment { get; set; }
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
        public int OfficeId { get; set; }
    }

    public class DocumentContext
    {
        public string RedirectTo { get; set; } = "public";
        public string? Tab { get; set; }
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

    public class DocumentSearchResult
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? Status { get; set; }
        public string? CreatorName { get; set; }
    }

    public class SearchDocumentsResponse
    {
        public List<DocumentSearchResult> Items { get; set; } = new();
        public int Total { get; set; }
    }

    public class GroupedSearchResult
    {
        public List<DocumentSearchResult> DocumentTracking { get; set; } = new();
        public int DocumentTrackingTotal { get; set; }
        public List<AuditSearchResult> AuditLog { get; set; } = new();
        public int AuditLogTotal { get; set; }
        public List<DocumentSearchResult> MyTracking { get; set; } = new();
        public int MyTrackingTotal { get; set; }
        public List<DocumentSearchResult> Incoming { get; set; } = new();
        public int IncomingTotal { get; set; }
        public List<DocumentSearchResult> OnDesk { get; set; } = new();
        public int OnDeskTotal { get; set; }
        public List<DocumentSearchResult> Outgoing { get; set; } = new();
        public int OutgoingTotal { get; set; }
        public List<NamedSearchResult> Offices { get; set; } = new();
        public int OfficesTotal { get; set; }
        public List<NamedSearchResult> Users { get; set; } = new();
        public int UsersTotal { get; set; }
    }

    public class AuditSearchResult
    {
        public int Id { get; set; }
        public string? DocumentName { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? Action { get; set; }
        public string? ByName { get; set; }
    }
    public class NamedSearchResult
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }
}
