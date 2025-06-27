using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Service
{
    public interface IGenericProjectService<TContext>
        where TContext : class
    {
        GenericProject<TContext> CreateProject(string name, ServiceName serviceName, string? description = null);
        List<GenericProject<TContext>> GetAllProjects();
        List<GenericProject<TContext>> GetProjectsByService(ServiceName serviceName);
        GenericProject<TContext>? GetProjectById(Guid projectId);
        void AddWorkflowToProject(Guid projectId, TContext workflow);

        TContext CreateWorkflow(Guid projectId, string configurationName, WorkflowType workflowType);
        List<TContext> GetWorkflowsByProject(Guid projectId);
        TContext? GetWorkflowById(Guid workflowId);
        void UpdateWorkflow(TContext workflow);
        List<TContext> GetAllWorkflows();
    }
}