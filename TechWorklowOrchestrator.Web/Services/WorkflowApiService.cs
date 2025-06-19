using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TechWorklowOrchestrator.Web.Models;

namespace TechWorklowOrchestrator.Web.Services
{
    public class WorkflowApiService : IWorkflowApiService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public WorkflowApiService(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("WorkflowAPI");
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public async Task<WorkflowSummary> GetSummaryAsync()
        {
            var response = await _httpClient.GetAsync("api/workflows/summary");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<WorkflowSummary>(json, _jsonOptions) ?? new WorkflowSummary();
        }

        public async Task<List<WorkflowResponse>> GetAllWorkflowsAsync()
        {
            var response = await _httpClient.GetAsync("api/workflows");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<WorkflowResponse>>(json, _jsonOptions) ?? new List<WorkflowResponse>();
        }

        public async Task<WorkflowResponse?> GetWorkflowAsync(Guid id)
        {
            var response = await _httpClient.GetAsync($"api/workflows/{id}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<WorkflowResponse>(json, _jsonOptions);
        }

        public async Task<WorkflowResponse> CreateWorkflowAsync(CreateWorkflowRequest request)
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("api/workflows", content);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<WorkflowResponse>(responseJson, _jsonOptions)!;
        }

        public async Task<WorkflowResponse> StartWorkflowAsync(Guid id)
        {
            var response = await _httpClient.PostAsync($"api/workflows/{id}/start", null);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<WorkflowResponse>(json, _jsonOptions)!;
        }

        public async Task<WorkflowResponse> HandleExternalEventAsync(Guid id, ExternalEventRequest eventRequest)
        {
            var json = JsonSerializer.Serialize(eventRequest, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"api/workflows/{id}/events", content);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<WorkflowResponse>(responseJson, _jsonOptions)!;
        }

        public async Task<bool> DeleteWorkflowAsync(Guid id)
        {
            var response = await _httpClient.DeleteAsync($"api/workflows/{id}");
            return response.IsSuccessStatusCode;
        }
    }
}
