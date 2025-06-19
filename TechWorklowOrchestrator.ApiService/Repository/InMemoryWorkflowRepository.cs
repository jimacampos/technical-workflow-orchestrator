using System.Collections.Concurrent;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Repository
{
    public class InMemoryWorkflowRepository : IWorkflowRepository
    {
        private readonly ConcurrentDictionary<Guid, WorkflowInstance> _workflows = new();

        public Task<Guid> CreateAsync(WorkflowInstance workflow)
        {
            workflow.Id = Guid.NewGuid();
            _workflows[workflow.Id] = workflow;
            return Task.FromResult(workflow.Id);
        }

        public Task<WorkflowInstance> GetByIdAsync(Guid id)
        {
            _workflows.TryGetValue(id, out var workflow);
            return Task.FromResult(workflow);
        }

        public Task<IEnumerable<WorkflowInstance>> GetAllAsync()
        {
            return Task.FromResult(_workflows.Values.AsEnumerable());
        }

        public Task<IEnumerable<WorkflowInstance>> GetByStateAsync(WorkflowState state)
        {
            var workflows = _workflows.Values.Where(w => w.CurrentState == state);
            return Task.FromResult(workflows);
        }

        public Task<IEnumerable<WorkflowInstance>> GetByTypeAsync(WorkflowType type)
        {
            var workflows = _workflows.Values.Where(w => w.WorkflowType == type);
            return Task.FromResult(workflows);
        }

        public Task UpdateAsync(WorkflowInstance workflow)
        {
            workflow.LastUpdated = DateTime.UtcNow;
            _workflows[workflow.Id] = workflow;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id)
        {
            _workflows.TryRemove(id, out _);
            return Task.CompletedTask;
        }
    }
}
