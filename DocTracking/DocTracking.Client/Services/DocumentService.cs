using System.Formats.Asn1;
using System.Net.Http.Json;
using DocTracking.Client.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace DocTracking.Client.Services
{
    public class DocumentService
    {
        private readonly HttpClient _http;

        public DocumentService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<Office>> GetOfficesAsync()
        {
            return await _http.GetFromJsonAsync<List<Office>>("api/offices") ?? new();
        }

        public async Task<List<Document>> GetAllDocumentsAsync()
        {
            return await _http.GetFromJsonAsync<List<Document>>("api/documents") ?? new();
        }

        public async Task CreateDocumentAsync(Document doc)
        {
            await _http.PostAsJsonAsync("api/documents", doc);
        }

        public async Task<string?> UploadFileAsync(IBrowserFile file)
        {
            using var content = new MultipartFormDataContent();

            var fileContent = new StreamContent(file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024));
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);

            content.Add(fileContent, "file", file.Name);

            var response = await _http.PostAsync("api/documents/upload", content);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
                return result?.FilePath;
            }

            return null;
        }
        

        public async Task AddOfficeAsync(Office office)
        {
            await _http.PostAsJsonAsync("api/offices", office);
        }

        public async Task<List<Document>> GetUserDocumentsAsync(string email)
        {
            return await _http.GetFromJsonAsync<List<Document>>($"api/documents/user/{email}") ?? new();
        }

        public async Task<List<Document>> GetIncomingAsync(int officeId, int? unitId = null)
        {
            var url = $"api/documents/incoming/{officeId}";

            if (unitId.HasValue)
            {
                url += $"?unitId={unitId.Value}";
            }

            return await _http.GetFromJsonAsync<List<Document>>(url) ?? new();
        }

        public async Task<AppUser?> GetProfileAsync()
        {
            var response = await _http.GetAsync("api/documents/my-profile");
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<AppUser>();
        }

        public async Task ReceivedDocumentAsync(int id)
        {
            await _http.PutAsync($"api/documents/{id}/receive", null);
        }
                                                                                                       
        public async Task ForwardDocumentAsync(int id, int nextOfficeId, int? nextUnitId = null)
        {
            var request = new { NextOfficeId = nextOfficeId, NextUnitId = nextUnitId };
            await _http.PutAsJsonAsync($"api/documents/{id}/forward", request);
        }

        public async Task FinishDocumentAsync(int id)
        {
            await _http.PutAsync($"api/documents/{id}/finish", null);
        }

        public async Task<List<DocumentLog>> GetDocumentLogsAsync(int id)
        {
           return await _http.GetFromJsonAsync<List<DocumentLog>>($"api/documentlogs/{id}") ?? new();
        }

            public async Task<List<AppUser>> GetAppUserAsync()
            {
            var response = await _http.GetAsync("api/appusers");

            if (!response.IsSuccessStatusCode)
                return new();

            return await response.Content.ReadFromJsonAsync<List<AppUser>>() ?? new();
        }

        public async Task UpdateAppUserAsync(AppUser user)
        {
            await _http.PutAsJsonAsync($"api/appusers/{user.Id}", user);
        }

        public async Task<List<Unit>> GetUnitsAsync()
        {
            return await _http.GetFromJsonAsync<List<Unit>>("api/units") ?? new();
        }

        public async Task AddUnitAsync(Unit unit)
        {
            await _http.PostAsJsonAsync("api/units", unit);
        }

        public async Task<List<Document>> GetOutgoingAsync(string email)
        {
            return await _http.GetFromJsonAsync<List<Document>>($"api/documents/outgoing/user/{email}") ?? new();
        }

        public async Task<List<Document>> GetUnitHistoryAsync(int unitId)
        {
            var response = await _http.GetAsync($"api/documents/history/unit/{unitId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<Document>>() ?? new List<Document>();
            }
            return new List<Document>();
        }


    }
        public class UploadResponse
        {
            public string? FilePath { get; set; }
        }
}
