using TechWorklowOrchestrator.ApiService.Dto;
using TechWorklowOrchestrator.ApiService.Repository;

namespace TechWorklowOrchestrator.ApiService.Service
{
    /// <summary>
    /// Interface for workflow-specific logic (to be implemented per workflow type).
    /// </summary>
    public interface IWorkflowProvider<TRequest, TContext>
    {
        TContext CreateContext(TRequest request);
        object CreateWorkflow(TContext context);
        Task<bool> HandleExternalEventAsync(object workflow, string eventType, Dictionary<string, object> data);
        Task<string> GetCurrentStatusAsync(object workflow);
        object GetCurrentState(object workflow);
        object GetContext(object workflow);
        WorkflowProgress CalculateProgress(GenericWorkflowInstance instance, object workflow);
        string GetDisplayName(object context); // e.g., PR title, update name, etc.
        string GetWorkflowType(object context); // e.g., "CodeUpdate", "DocumentApproval"
        public Dictionary<string, string> GetMetaData(object context); // e.g., PR title, update name, etc.
    }
}