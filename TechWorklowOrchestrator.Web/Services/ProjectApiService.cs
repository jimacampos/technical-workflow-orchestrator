using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TechWorklowOrchestrator.Web.Models;

namespace TechWorklowOrchestrator.Web.Services
{
    public class ProjectApiService : IProjectApiService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public ProjectApiService(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("WorkflowAPI"); // Using same client as your WorkflowApiService
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public async Task<List<ProjectResponse>> GetAllProjectsAsync()
        {
            var response = await _httpClient.GetAsync("api/projects");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ProjectResponse>>(json, _jsonOptions) ?? new List<ProjectResponse>();
        }

        public async Task<ProjectResponse?> GetProjectAsync(Guid id)
        {
            var response = await _httpClient.GetAsync($"api/projects/{id}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ProjectResponse>(json, _jsonOptions);
        }

        public async Task<List<ProjectResponse>> GetProjectsByServiceAsync(ServiceName serviceName)
        {
            var response = await _httpClient.GetAsync($"api/projects/service/{serviceName}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ProjectResponse>>(json, _jsonOptions) ?? new List<ProjectResponse>();
        }

        public async Task<ProjectResponse> CreateProjectAsync(CreateProjectRequest request)
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("api/projects", content);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ProjectResponse>(responseJson, _jsonOptions)!;
        }

        public async Task<List<WorkflowResponse>> GetWorkflowsByProjectAsync(Guid projectId)
        {
            var response = await _httpClient.GetAsync($"api/projects/{projectId}/workflows");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<WorkflowResponse>>(json, _jsonOptions) ?? new List<WorkflowResponse>();
        }

        public async Task<WorkflowResponse> CreateWorkflowInProjectAsync(Guid projectId, CreateWorkflowRequest request)
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"api/projects/{projectId}/workflows", content);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<WorkflowResponse>(responseJson, _jsonOptions)!;
        }
    }
}
