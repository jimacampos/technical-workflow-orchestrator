using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Repository
{
    public interface IWorkflowRepository
    {
        Task<Guid> CreateAsync(WorkflowInstance workflow);
        Task<WorkflowInstance> GetByIdAsync(Guid id);
        Task<IEnumerable<WorkflowInstance>> GetAllAsync();
        Task<IEnumerable<WorkflowInstance>> GetByStateAsync(WorkflowState state);
        Task<IEnumerable<WorkflowInstance>> GetByTypeAsync(WorkflowType type);
        Task UpdateAsync(WorkflowInstance workflow);
        Task DeleteAsync(Guid id);
    }
}
