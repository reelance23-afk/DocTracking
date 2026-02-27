using System.Formats.Asn1;
using System.Net.Http.Json;
using DocTracking.Client.Models;

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

        public async Task AddOfficeAsync(Office office)
        {
            await _http.PostAsJsonAsync("api/offices", office);
        }

        public async Task<List<Document>> GetUserDocumentsAsync(string email)
        {
            return await _http.GetFromJsonAsync<List<Document>>($"api/documents/user/{email}") ?? new();
        }

        public async Task<List<Document>> GetIncomingAsync(int officeId)
        {
            return await _http.GetFromJsonAsync<List<Document>>($"api/documents/incoming/{officeId}") ?? new();
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

    }
}
