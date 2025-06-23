using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Repository
{
    public interface IProjectRepository
    {
        // Project operations
        Task<Guid> CreateProjectAsync(Project project);
        Task<Project> GetProjectByIdAsync(Guid id);
        Task<IEnumerable<Project>> GetAllProjectsAsync();
        Task<IEnumerable<Project>> GetProjectsByServiceAsync(ServiceName serviceName);
        Task UpdateProjectAsync(Project project);
        Task DeleteProjectAsync(Guid id);

        // Workflow operations
        Task<Guid> CreateWorkflowAsync(ConfigCleanupContext workflow);
        Task<ConfigCleanupContext> GetWorkflowByIdAsync(Guid id);
        Task<IEnumerable<ConfigCleanupContext>> GetAllWorkflowsAsync();
        Task<IEnumerable<ConfigCleanupContext>> GetWorkflowsByProjectAsync(Guid projectId);
        Task<IEnumerable<ConfigCleanupContext>> GetWorkflowsByTypeAsync(WorkflowType workflowType);
        Task UpdateWorkflowAsync(ConfigCleanupContext workflow);
        Task DeleteWorkflowAsync(Guid id);

        // Utility methods
        Task<bool> ProjectExistsAsync(Guid projectId);
        Task<int> GetProjectCountAsync();
        Task<int> GetWorkflowCountAsync();
        Task<int> GetWorkflowCountByProjectAsync(Guid projectId);
    }
}
