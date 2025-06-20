using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Service
{
    public interface IProjectService
    {
        Project CreateProject(string name, ServiceName serviceName, string? description = null);
        List<Project> GetAllProjects();
        List<Project> GetProjectsByService(ServiceName serviceName);
        Project? GetProjectById(Guid projectId);
        void AddWorkflowToProject(Guid projectId, ConfigCleanupContext workflow);

        ConfigCleanupContext CreateWorkflow(Guid projectId, string configurationName, WorkflowType workflowType);
        List<ConfigCleanupContext> GetWorkflowsByProject(Guid projectId);
        ConfigCleanupContext? GetWorkflowById(Guid workflowId);
        void UpdateWorkflow(ConfigCleanupContext workflow);
        List<ConfigCleanupContext> GetAllWorkflows();
    }
}
