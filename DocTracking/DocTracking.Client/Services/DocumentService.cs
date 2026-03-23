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
            var response = await _http.GetAsync(url);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();
            if (!response.IsSuccessStatusCode) return default;
            var content = await response.Content.ReadAsStringAsync();
            if (content.TrimStart().StartsWith('<')) return default;
            return JsonSerializer.Deserialize<T>(content, _jsonOptions);
        }

        private async Task<(bool Success, string? Error)> ToResult(Task<HttpResponseMessage> task)
        {
            var response = await task;
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }

        public async Task<List<Office>> GetOfficesAsync() =>
            await GetJsonAsync<List<Office>>("api/offices") ?? new();

        public async Task<PagedResult<Document>> GetAllDocumentsAsync(
            int page = 1, int pageSize = 25,
            string? search = null, string? status = null,
            string? office = null, string? dateFrom = null, string? dateTo = null)
        {
            var url = $"api/documents?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
            if (!string.IsNullOrEmpty(status)) url += $"&status={Uri.EscapeDataString(status)}";
            if (!string.IsNullOrEmpty(office)) url += $"&office={Uri.EscapeDataString(office)}";
            if (!string.IsNullOrEmpty(dateFrom)) url += $"&dateFrom={Uri.EscapeDataString(dateFrom)}";
            if (!string.IsNullOrEmpty(dateTo)) url += $"&dateTo={Uri.EscapeDataString(dateTo)}";
            return await GetJsonAsync<PagedResult<Document>>(url) ?? new();
        }

        public async Task<List<Document>> GetUserDocumentsAsync(string email) =>
            await GetJsonAsync<List<Document>>($"api/documents/user/{email}") ?? new();

        public async Task<List<Document>> GetIncomingAsync(int officeId, int? unitId = null)
        {
            var url = $"api/documents/incoming/{officeId}";
            if (unitId.HasValue) url += $"?unitId={unitId.Value}";
            return await GetJsonAsync<List<Document>>(url) ?? new();
        }

        public async Task<List<Document>> GetDeskDocumentsAsync(int officeId, int? unitId = null)
        {
            var url = $"api/documents/desk/{officeId}";
            if (unitId.HasValue) url += $"?unitId={unitId.Value}";
            return await GetJsonAsync<List<Document>>(url) ?? new();
        }

        public async Task<List<Document>> GetOutgoingAsync() =>
            await GetJsonAsync<List<Document>>("api/documents/outgoing/user") ?? new();

        public async Task<List<DocumentLog>> GetDocumentLogsAsync(int id) =>
            await GetJsonAsync<List<DocumentLog>>($"api/documentlogs/{id}") ?? new();

        public async Task<PagedResult<DocumentLog>> GetAuditLogsAsync(
            int page = 1, int pageSize = 25,
            string? search = null, string? action = null, string? date = null)
        {
            var url = $"api/documentlogs/audit?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
            if (!string.IsNullOrEmpty(action)) url += $"&action={Uri.EscapeDataString(action)}";
            if (!string.IsNullOrEmpty(date)) url += $"&date={Uri.EscapeDataString(date)}";
            return await GetJsonAsync<PagedResult<DocumentLog>>(url) ?? new();
        }

        public async Task<List<AppUser>> GetAppUserAsync() =>
            await GetJsonAsync<List<AppUser>>("api/appusers") ?? new();

        public async Task<List<Unit>> GetUnitsAsync() =>
            await GetJsonAsync<List<Unit>>("api/units") ?? new();

        public async Task<List<Document>> GetUnitHistoryAsync(int unitId) =>
            await GetJsonAsync<List<Document>>($"api/documents/history/unit/{unitId}") ?? new();

        public async Task<List<Document>> GetOfficeHistoryAsync(int officeId) =>
            await GetJsonAsync<List<Document>>($"api/documents/history/office/{officeId}") ?? new();

        public async Task<List<Document>> GetUserActivityAsync(int userId) =>
            await GetJsonAsync<List<Document>>($"api/documents/activity/user/{userId}") ?? new();

        public async Task<AppUser?> GetProfileAsync()
        {
            var response = await _http.GetAsync("api/documents/my-profile");
            if (!response.IsSuccessStatusCode) return null;
            var content = await response.Content.ReadAsStringAsync();
            if (content.TrimStart().StartsWith('<')) return null;
            return JsonSerializer.Deserialize<AppUser>(content, _jsonOptions);
        }

        public Task<(bool Success, string? Error)> CreateDocumentAsync(Document doc) =>
            ToResult(_http.PostAsJsonAsync("api/documents", doc));

        public async Task<string?> UploadFileAsync(IBrowserFile file)
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

        public async Task DeleteUploadAsync(string path) =>
            await _http.DeleteAsync($"api/documents/upload?path={Uri.EscapeDataString(path)}");

        public Task<(bool Success, string? Error)> ReceivedDocumentAsync(int id) =>
            ToResult(_http.PutAsync($"api/documents/{id}/receive", null));

        public Task<(bool Success, string? Error)> ForwardDocumentAsync(int id, int nextOfficeId, int? nextUnitId = null, string? comment = null) =>
            ToResult(_http.PutAsJsonAsync($"api/documents/{id}/forward", new { NextOfficeId = nextOfficeId, NextUnitId = nextUnitId, Comment = comment }));

        public Task<(bool Success, string? Error)> FinishDocumentAsync(int id, string? comment = null) =>
            ToResult(_http.PutAsJsonAsync($"api/documents/{id}/finish", new { Comment = comment }));

        public Task<(bool Success, string? Error)> UpdateAppUserAsync(AppUser user) =>
            ToResult(_http.PutAsJsonAsync($"api/appusers/{user.Id}", user));

        public Task<(bool Success, string? Error)> AddOfficeAsync(Office office) =>
            ToResult(_http.PostAsJsonAsync("api/offices", office));

        public Task<(bool Success, string? Error)> UpdateOfficeAsync(Office office) =>
            ToResult(_http.PutAsJsonAsync($"api/offices/{office.Id}", office));

        public Task<(bool Success, string? Error)> DeleteOfficeAsync(int id) =>
            ToResult(_http.DeleteAsync($"api/offices/{id}"));

        public Task<(bool Success, string? Error)> AddUnitAsync(Unit unit) =>
            ToResult(_http.PostAsJsonAsync("api/units", unit));

        public Task<(bool Success, string? Error)> UpdateUnitAsync(Unit unit) =>
            ToResult(_http.PutAsJsonAsync($"api/units/{unit.Id}", unit));

        public Task<(bool Success, string? Error)> DeleteUnitAsync(int id) =>
            ToResult(_http.DeleteAsync($"api/units/{id}"));

        public Task<(bool Success, string? Error)> DeleteAppUserAsync(int id) =>
            ToResult(_http.DeleteAsync($"api/appusers/{id}"));

        public Task<(bool Success, string? Error)> BulkReassignUsersAsync(int fromUnitId, int toUnitId) =>
            ToResult(_http.PutAsJsonAsync("api/appusers/bulk-reassign", new { FromUnitId = fromUnitId, ToUnitId = toUnitId }));

        public Task<(bool Success, string? Error)> SelectiveReassignUsersAsync(List<int> userIds, int toUnitId) =>
            ToResult(_http.PutAsJsonAsync("api/appusers/selective-reassign", new { UserIds = userIds, ToUnitId = toUnitId }));

        public Task<(bool Success, string? Error)> BroadcastNotificationAsync(string message, int? officeId = null) =>
            ToResult(_http.PostAsJsonAsync("api/notifications/broadcast", new { Message = message, OfficeId = officeId }));

        public Task<(bool Success, string? Error)> UpdateDocumentAsync(Document doc) =>
            ToResult(_http.PutAsJsonAsync($"api/documents/{doc.Id}", doc));

        public Task<(bool Success, string? Error)> DeleteDocumentAsync(int id) =>
            ToResult(_http.DeleteAsync($"api/documents/{id}"));

        public Task<(bool Success, string? Error)> AdminOverrideDocumentAsync(int id, AdminOverridePayload request) =>
            ToResult(_http.PutAsJsonAsync($"api/documents/{id}/admin-override", request));

        public string GetQRCodeUrl(int documentId) => $"{_http.BaseAddress}api/documents/{documentId}/qrcode";

        public async Task<Document?> GetDocumentByRefAsync(string referenceNumber)
        {
            var response = await _http.GetAsync($"api/documents/by-ref/{referenceNumber}");
            if (!response.IsSuccessStatusCode) return null;
            var content = await response.Content.ReadAsStringAsync();
            if (content.TrimStart().StartsWith('<')) return null;
            return JsonSerializer.Deserialize<Document>(content, _jsonOptions);
        }

        public Task<(bool Success, string? Error)> BulkDeleteDocumentsAsync(List<int> ids) =>
        ToResult(_http.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "api/documents/bulk")
        {
            Content = JsonContent.Create(ids)
        }));


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
}
