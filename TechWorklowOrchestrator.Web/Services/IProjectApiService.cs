using TechWorklowOrchestrator.Web.Models;

namespace TechWorklowOrchestrator.Web.Services
{
    public interface IProjectApiService
    {
        Task<List<ProjectResponse>> GetAllProjectsAsync();
        Task<ProjectResponse?> GetProjectAsync(Guid id);
        Task<List<ProjectResponse>> GetProjectsByServiceAsync(ServiceName serviceName);
        Task<ProjectResponse> CreateProjectAsync(CreateProjectRequest request);

        Task<List<WorkflowResponse>> GetWorkflowsByProjectAsync(Guid projectId);
        Task<WorkflowResponse> CreateWorkflowInProjectAsync(Guid projectId, CreateWorkflowRequest request);
    }
}
