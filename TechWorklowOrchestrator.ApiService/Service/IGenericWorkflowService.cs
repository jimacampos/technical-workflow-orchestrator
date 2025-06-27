using TechWorklowOrchestrator.ApiService.Dto;

namespace TechWorklowOrchestrator.ApiService.Service
{
    public interface IGenericWorkflowService<TRequest, TContext>
    {
        Task<Guid> CreateWorkflowAsync(TRequest request);
        Task<WorkflowResponse> GetWorkflowAsync(Guid id);
        Task<IEnumerable<WorkflowResponse>> GetAllWorkflowsAsync();
        Task<WorkflowResponse> StartWorkflowAsync(Guid id);
        Task<WorkflowResponse> HandleExternalEventAsync(Guid id, ExternalEventRequest eventRequest);
        Task<WorkflowSummary> GetSummaryAsync();
        Task<bool> DeleteWorkflowAsync(Guid id);
        Task<WorkflowResponse> ProceedWorkflowAsync(Guid id);
    }
}
