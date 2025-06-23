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
            var combinedSummary = new WorkflowSummary();

            try
            {
                // Get standalone workflow summary
                var response = await _httpClient.GetAsync("api/workflows/summary");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var standaloneSummary = JsonSerializer.Deserialize<WorkflowSummary>(json, _jsonOptions);
                    if (standaloneSummary != null)
                    {
                        combinedSummary.TotalWorkflows += standaloneSummary.TotalWorkflows;
                        combinedSummary.ActiveWorkflows += standaloneSummary.ActiveWorkflows;
                        combinedSummary.CompletedWorkflows += standaloneSummary.CompletedWorkflows;
                        combinedSummary.FailedWorkflows += standaloneSummary.FailedWorkflows;
                        combinedSummary.AwaitingManualAction += standaloneSummary.AwaitingManualAction;

                        // Combine ByType dictionaries
                        foreach (var kvp in standaloneSummary.ByType)
                        {
                            combinedSummary.ByType[kvp.Key] = combinedSummary.ByType.GetValueOrDefault(kvp.Key) + kvp.Value;
                        }

                        // Combine ByState dictionaries
                        foreach (var kvp in standaloneSummary.ByState)
                        {
                            combinedSummary.ByState[kvp.Key] = combinedSummary.ByState.GetValueOrDefault(kvp.Key) + kvp.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading standalone workflow summary: {ex.Message}");
            }

            try
            {
                // Get project workflow summary
                var projectResponse = await _httpClient.GetAsync("api/projects/workflows/summary");
                if (projectResponse.IsSuccessStatusCode)
                {
                    var projectJson = await projectResponse.Content.ReadAsStringAsync();
                    var projectSummary = JsonSerializer.Deserialize<WorkflowSummary>(projectJson, _jsonOptions);
                    if (projectSummary != null)
                    {
                        combinedSummary.TotalWorkflows += projectSummary.TotalWorkflows;
                        combinedSummary.ActiveWorkflows += projectSummary.ActiveWorkflows;
                        combinedSummary.CompletedWorkflows += projectSummary.CompletedWorkflows;
                        combinedSummary.FailedWorkflows += projectSummary.FailedWorkflows;
                        combinedSummary.AwaitingManualAction += projectSummary.AwaitingManualAction;

                        // Combine ByType dictionaries
                        foreach (var kvp in projectSummary.ByType)
                        {
                            combinedSummary.ByType[kvp.Key] = combinedSummary.ByType.GetValueOrDefault(kvp.Key) + kvp.Value;
                        }

                        // Combine ByState dictionaries
                        foreach (var kvp in projectSummary.ByState)
                        {
                            combinedSummary.ByState[kvp.Key] = combinedSummary.ByState.GetValueOrDefault(kvp.Key) + kvp.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading project workflow summary: {ex.Message}");
            }

            return combinedSummary;
        }

        public async Task<List<WorkflowResponse>> GetAllWorkflowsAsync()
        {
            var allWorkflows = new List<WorkflowResponse>();

            try
            {
                // Get standalone workflows from original endpoint
                var response = await _httpClient.GetAsync("api/workflows");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var standaloneWorkflows = JsonSerializer.Deserialize<List<WorkflowResponse>>(json, _jsonOptions) ?? new List<WorkflowResponse>();
                    allWorkflows.AddRange(standaloneWorkflows);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading standalone workflows: {ex.Message}");
            }

            try
            {
                // Get project-based workflows from new endpoint
                var projectResponse = await _httpClient.GetAsync("api/projects/workflows");
                if (projectResponse.IsSuccessStatusCode)
                {
                    var projectJson = await projectResponse.Content.ReadAsStringAsync();
                    var projectWorkflows = JsonSerializer.Deserialize<List<WorkflowResponse>>(projectJson, _jsonOptions) ?? new List<WorkflowResponse>();
                    allWorkflows.AddRange(projectWorkflows);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading project workflows: {ex.Message}");
            }

            return allWorkflows;
        }

        public async Task<WorkflowResponse?> GetWorkflowAsync(Guid id)
        {
            // First try the project-based workflow endpoint (which has stage progress)
            var projectResponse = await _httpClient.GetAsync($"api/projects/workflows/{id}");
            if (projectResponse.IsSuccessStatusCode)
            {
                var projectJson = await projectResponse.Content.ReadAsStringAsync();
                var workflowFromProject = JsonSerializer.Deserialize<WorkflowResponse>(projectJson, _jsonOptions);
                if (workflowFromProject != null)
                {
                    return workflowFromProject;
                }
            }

            // If not found in projects, try the original workflow endpoint
            var response = await _httpClient.GetAsync($"api/workflows/{id}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<WorkflowResponse>(json, _jsonOptions);
            }

            // Not found in either endpoint
            return null;
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
            // First try the project-based workflow start endpoint
            try
            {
                var projectResponse = await _httpClient.PostAsync($"api/projects/workflows/{id}/start", null);
                if (projectResponse.IsSuccessStatusCode)
                {
                    var projectJson = await projectResponse.Content.ReadAsStringAsync();
                    var workflowFromProject = JsonSerializer.Deserialize<WorkflowResponse>(projectJson, _jsonOptions);
                    if (workflowFromProject != null)
                    {
                        return workflowFromProject;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Project workflow start failed: {ex.Message}");
            }

            // If project endpoint fails, try the original workflow endpoint
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

        public async Task<WorkflowResponse?> ProceedWorkflowAsync(Guid projectId, Guid workflowId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/projects/{projectId}/workflows/{workflowId}/proceed", null);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<WorkflowResponse>(json, _jsonOptions);
                }
                else
                {
                    Console.WriteLine($"Proceed workflow failed with status: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error proceeding workflow: {ex.Message}");
                throw;
            }
        }

        public async Task<WorkflowResponse?> SetPullRequestUrlAsync(Guid projectId, Guid workflowId, string pullRequestUrl)
        {
            var request = new { PullRequestUrl = pullRequestUrl };
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"api/projects/{projectId}/workflows/{workflowId}/pullrequest", content);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<WorkflowResponse>(responseJson, _jsonOptions);
        }

    }
}
