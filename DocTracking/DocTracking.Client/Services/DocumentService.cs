using System.Net.Http.Json;
using System.Text.Json;
using DocTracking.Client.Models;
using Microsoft.AspNetCore.Components.Forms;

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

        public async Task<List<Office>> GetOfficesAsync() =>
            await GetJsonAsync<List<Office>>("api/offices") ?? new();

        public async Task<List<Document>> GetAllDocumentsAsync() =>
            await GetJsonAsync<List<Document>>("api/documents") ?? new();

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

        public async Task<List<AppUser>> GetAppUserAsync() =>
            await GetJsonAsync<List<AppUser>>("api/appusers") ?? new();

        public async Task<List<Unit>> GetUnitsAsync() =>
            await GetJsonAsync<List<Unit>>("api/units") ?? new();

        public async Task<List<Document>> GetUnitHistoryAsync(int unitId) =>
            await GetJsonAsync<List<Document>>($"api/documents/history/unit/{unitId}") ?? new();

        public async Task<List<Document>> GetOfficeHistoryAsync(int officeId) =>
            await GetJsonAsync<List<Document>>($"api/documents/history/office/{officeId}") ?? new();

        public async Task<AppUser?> GetProfileAsync()
        {
            var response = await _http.GetAsync("api/documents/my-profile");
            if (!response.IsSuccessStatusCode) return null;
            var content = await response.Content.ReadAsStringAsync();
            if (content.TrimStart().StartsWith('<')) return null;
            return JsonSerializer.Deserialize<AppUser>(content, _jsonOptions);
        }

        public async Task<bool> CreateDocumentAsync(Document doc)
        {
            var response = await _http.PostAsJsonAsync("api/documents", doc);
            return response.IsSuccessStatusCode;
        }

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

        public async Task<bool> ReceivedDocumentAsync(int id) =>
            (await _http.PutAsync($"api/documents/{id}/receive", null)).IsSuccessStatusCode;

        public async Task<(bool Success, string? Error)> ForwardDocumentAsync(int id, int nextOfficeId, int? nextUnitId = null, string? comment = null)
        {
           var response = await _http.PutAsJsonAsync($"api/documents/{id}/forward", new { NextOfficeId = nextOfficeId, NextUnitId = nextUnitId, Comment = comment });
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }

        public async Task<(bool Success, string? Error)> FinishDocumentAsync(int id, string? comment = null)
        {
            var response = await _http.PutAsJsonAsync($"api/documents/{id}/finish", new { Comment = comment });
            if (response.IsSuccessStatusCode) return (true, null); 
            return (false, await response.Content.ReadAsStringAsync());
        }


        public async Task<(bool Success, string? Error)> UpdateAppUserAsync(AppUser user)
        {
           var response = await _http.PutAsJsonAsync($"api/appusers/{user.Id}", user);
           if (response.IsSuccessStatusCode) return (true, null);
           return (false, await response.Content.ReadAsStringAsync());

        }

        public async Task<(bool Success, string? Error)> AddOfficeAsync(Office office)
        {
            var response = await _http.PostAsJsonAsync("api/offices", office);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }

        public async Task<(bool Success, string? Error)> UpdateOfficeAsync(Office office)
        {
            var response = await _http.PutAsJsonAsync($"api/offices/{office.Id}", office);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }

        public async Task<(bool Success, string? Error)> DeleteOfficeAsync(int id)
        {
            var response = await _http.DeleteAsync($"api/offices/{id}");
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }

        public async Task<(bool Success, string? Error)> AddUnitAsync(Unit unit)
        {
            var response = await _http.PostAsJsonAsync("api/units", unit);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }

        public async Task<(bool Success, string? Error)> UpdateUnitAsync(Unit unit)
        {
            var response = await _http.PutAsJsonAsync($"api/units/{unit.Id}", unit);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }

        public async Task<(bool Success, string? Error)> DeleteUnitAsync(int id)
        {
            var response = await _http.DeleteAsync($"api/units/{id}");
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }

        public async Task<(bool Success, string? Error)> DeleteAppUserAsync(int id)
        {
            var response = await _http.DeleteAsync($"api/appusers/{id}");
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }

        public async Task<(bool Success, string? Error)> UpdateDocumentAsync(Document doc)
        {
            var response = await _http.PutAsJsonAsync($"api/documents/{doc.Id}", doc);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }

        public async Task<(bool Success, string? Error)> DeleteDocumentAsync(int id)
        {
            var response = await _http.DeleteAsync($"api/documents/{id}");
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }

        public string GetQRCodeUrl(int documentId) => $"{_http.BaseAddress}api/documents/{documentId}/qrcode";

        public async Task<Document?> GetDocumentByRefAsync(string referenceNumber)
        {
            var response = await _http.GetAsync($"api/documents/by-ref/{referenceNumber}");
            if (!response.IsSuccessStatusCode) return null;
            var content = await response.Content.ReadAsStringAsync();
            if (content.TrimStart().StartsWith('<')) return null;
            return JsonSerializer.Deserialize<Document>(content, _jsonOptions);
        }

    }

    public class UploadResponse
    {
        public string? FilePath { get; set; }
    }
}