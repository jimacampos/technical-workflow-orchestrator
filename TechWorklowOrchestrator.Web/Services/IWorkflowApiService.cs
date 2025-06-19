using TechWorklowOrchestrator.Web.Models;

namespace TechWorklowOrchestrator.Web.Services
{
    public interface IWorkflowApiService
    {
        Task<WorkflowSummary> GetSummaryAsync();
        Task<List<WorkflowResponse>> GetAllWorkflowsAsync();
        Task<WorkflowResponse?> GetWorkflowAsync(Guid id);
        Task<WorkflowResponse> CreateWorkflowAsync(CreateWorkflowRequest request);
        Task<WorkflowResponse> StartWorkflowAsync(Guid id);
        Task<WorkflowResponse> HandleExternalEventAsync(Guid id, ExternalEventRequest eventRequest);
        Task<bool> DeleteWorkflowAsync(Guid id);
    }
}
