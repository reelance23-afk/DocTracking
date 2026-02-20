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
                                                                                                       
        public async Task ForwardDocumentAsync(int id, int nextOfficeId)
        {
            await _http.PutAsJsonAsync($"api/documents/{id}/forward", nextOfficeId);
        }

        public async Task<List<DocumentLog>> GetDocumentLogsAsync(int id)
        {
           return await _http.GetFromJsonAsync<List<DocumentLog>>($"api/documentlogs/{id}") ?? new();
        }

    }
}
