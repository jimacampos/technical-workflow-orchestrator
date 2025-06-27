using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TechWorklowOrchestrator.ApiService.Repository
{
    public interface IGenericWorkflowRepository
    {
        Task<Guid> CreateAsync(GenericWorkflowInstance workflow);
        Task<GenericWorkflowInstance> GetByIdAsync(Guid id);
        Task<IEnumerable<GenericWorkflowInstance>> GetAllAsync();
        Task<IEnumerable<GenericWorkflowInstance>> GetByTypeAsync(string workflowType);
        Task<IEnumerable<GenericWorkflowInstance>> GetByDisplayNameAsync(string displayName);
        Task UpdateAsync(GenericWorkflowInstance workflow);
        Task DeleteAsync(Guid id);
    }
}