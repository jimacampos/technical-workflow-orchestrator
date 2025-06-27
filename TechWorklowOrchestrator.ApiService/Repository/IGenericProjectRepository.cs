using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Repository
{
    public interface IGenericProjectRepository<TContext>
        where TContext : class
    {
        // Project operations
        Task<Guid> CreateProjectAsync(GenericProject<TContext> project);
        Task<GenericProject<TContext>> GetProjectByIdAsync(Guid id);
        Task<IEnumerable<GenericProject<TContext>>> GetAllProjectsAsync();
        Task<IEnumerable<GenericProject<TContext>>> GetProjectsByServiceAsync(ServiceName serviceName);
        Task UpdateProjectAsync(GenericProject<TContext> project);
        Task DeleteProjectAsync(Guid id);

        // Workflow operations
        Task<Guid> CreateWorkflowAsync(TContext workflow);
        Task<TContext> GetWorkflowByIdAsync(Guid id);
        Task<IEnumerable<TContext>> GetAllWorkflowsAsync();
        Task<IEnumerable<TContext>> GetWorkflowsByProjectAsync(Guid projectId);
        Task<IEnumerable<TContext>> GetWorkflowsByTypeAsync(WorkflowType workflowType);
        Task UpdateWorkflowAsync(TContext workflow);
        Task DeleteWorkflowAsync(Guid id);

        // Utility methods
        Task<bool> ProjectExistsAsync(Guid projectId);
        Task<int> GetProjectCountAsync();
        Task<int> GetWorkflowCountAsync();
        Task<int> GetWorkflowCountByProjectAsync(Guid projectId);

        // Factory/helper methods for generic service support
        GenericProject<TContext> CreateProjectInstance(string name, ServiceName serviceName, string? description = null);
        TContext CreateWorkflowInstance(Guid projectId, string configurationName, WorkflowType workflowType);
        void SetWorkflowProjectId(TContext workflow, Guid projectId);
        void AddWorkflowToProject(GenericProject<TContext> project, TContext workflow);
    }
}