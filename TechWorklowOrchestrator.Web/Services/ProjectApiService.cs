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
            _httpClient = httpClientFactory.CreateClient("WorkflowAPI");
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public async Task<List<ProjectResponse>> GetAllProjectsAsync()
        {
            var response = await _httpClient.GetAsync("api/all-projects");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ProjectResponse>>(json, _jsonOptions) ?? new List<ProjectResponse>();
        }

        public async Task<ProjectResponse?> GetProjectAsync(Guid id)
        {
            var response = await _httpClient.GetAsync($"api/all-projects/{id}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ProjectResponse>(json, _jsonOptions);
        }

        public async Task<List<ProjectResponse>> GetProjectsByServiceAsync(ServiceName serviceName)
        {
            var response = await _httpClient.GetAsync($"api/all-projects/service/{serviceName}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ProjectResponse>>(json, _jsonOptions) ?? new List<ProjectResponse>();
        }

        public async Task<ProjectResponse> CreateProjectAsync(CreateProjectRequest request)
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            string endpoint = request.ProjectType switch
            {
                ProjectType.ConfigCleanup => "api/config-projects",
                ProjectType.CodeUpdate => "api/codeupdate-projects",
                _ => throw new InvalidOperationException("Unknown project type")
            };
            var response = await _httpClient.PostAsync(endpoint, content);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ProjectResponse>(responseJson, _jsonOptions)!;
        }

        public async Task<List<WorkflowResponse>> GetWorkflowsByProjectAsync(Guid projectId)
        {
            var response = await _httpClient.GetAsync($"api/all-projects/{projectId}/workflows");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<WorkflowResponse>>(json, _jsonOptions) ?? new List<WorkflowResponse>();
        }

        public async Task<WorkflowResponse> CreateWorkflowInProjectAsync(Guid projectId, CreateWorkflowRequest request)
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"api/config-projects/{projectId}/workflows", content);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            Console.WriteLine("[DEBUG] Raw response JSON: " + responseJson); // 👈 LOG THIS

            try
            {
                var workflow = JsonSerializer.Deserialize<WorkflowResponse>(responseJson, _jsonOptions)!;
                return workflow;
            }
            catch (JsonException ex)
            {
                Console.WriteLine("[ERROR] Deserialization failed: " + ex.Message);
                throw;
            }
        }

        public async Task<WorkflowResponse> CreateWorkflowInProjectAsync(Guid projectId, CodeUpdateWorkflowRequest request)
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"api/codeupdate-projects/{projectId}/workflows", content);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<WorkflowResponse>(responseJson, _jsonOptions)!;
        }
    }
}
