using System.Collections.Concurrent;
using TechWorklowOrchestrator.ApiService.Repository;

namespace TechWorklowOrchestrator.ApiService.Repository
{
    public class InMemoryGenericWorkflowRepository : IGenericWorkflowRepository
    {
        private readonly ConcurrentDictionary<Guid, GenericWorkflowInstance> _workflows = new();

        public Task<Guid> CreateAsync(GenericWorkflowInstance workflow)
        {
            workflow.Id = Guid.NewGuid();
            _workflows[workflow.Id] = workflow;
            return Task.FromResult(workflow.Id);
        }

        public Task<GenericWorkflowInstance> GetByIdAsync(Guid id)
        {
            _workflows.TryGetValue(id, out var workflow);
            return Task.FromResult(workflow);
        }

        public Task<IEnumerable<GenericWorkflowInstance>> GetAllAsync()
        {
            return Task.FromResult(_workflows.Values.AsEnumerable());
        }

        public Task<IEnumerable<GenericWorkflowInstance>> GetByTypeAsync(string workflowType)
        {
            var workflows = _workflows.Values
                .Where(w => string.Equals(w.WorkflowType, workflowType, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(workflows);
        }

        public Task<IEnumerable<GenericWorkflowInstance>> GetByDisplayNameAsync(string displayName)
        {
            var workflows = _workflows.Values
                .Where(w => string.Equals(w.WorkflowDisplayName, displayName, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(workflows);
        }

        public Task UpdateAsync(GenericWorkflowInstance workflow)
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