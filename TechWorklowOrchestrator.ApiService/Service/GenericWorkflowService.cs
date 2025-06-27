using System.Collections.Concurrent;
using TechWorklowOrchestrator.ApiService.Dto;
using TechWorklowOrchestrator.ApiService.Repository;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Service
{
    /// <summary>
    /// Generic, workflow-agnostic service for managing workflows.
    /// </summary>
    public class GenericWorkflowService<TRequest, TContext> : IGenericWorkflowService<TRequest, TContext>
    {
        private readonly IGenericWorkflowRepository _repository;
        private readonly IWorkflowProvider<TRequest, TContext> _provider;

        // Shared across all generic workflow service instances
        private readonly ConcurrentDictionary<Guid, object> _activeWorkflows = new();

        /// <summary>
        /// Gets the total number of active workflows across all types.
        /// </summary>
        public int TotalActiveWorkflows => _activeWorkflows.Count;

        /// <summary>
        /// Returns all active workflow objects.
        /// </summary>
        public IEnumerable<object> GetAllActiveWorkflows() => _activeWorkflows.Values;

        public GenericWorkflowService(
            IGenericWorkflowRepository repository,
            IWorkflowProvider<TRequest, TContext> provider)
        {
            _repository = repository;
            _provider = provider;
        }

        public async Task<Guid> CreateWorkflowAsync(TRequest request)
        {
            var context = _provider.CreateContext(request);
            var workflow = _provider.CreateWorkflow(context);

            var instance = new GenericWorkflowInstance
            {
                WorkflowDisplayName = _provider.GetDisplayName(context),
                WorkflowType = _provider.GetWorkflowType(context),
                CurrentState = _provider.GetCurrentState(workflow),
                Context = context,
                Metadata = _provider.GetMetaData(request)
            };

            instance.History.Add(new
            {
                Timestamp = DateTime.UtcNow,
                EventType = "WorkflowCreated",
                FromState = instance.CurrentState,
                ToState = instance.CurrentState,
                Description = $"Workflow created: {instance.WorkflowDisplayName}",
                Data = new Dictionary<string, object>()
            });

            var id = await _repository.CreateAsync(instance);
            _activeWorkflows[id] = workflow;
            return id;
        }

        public async Task<WorkflowResponse> GetWorkflowAsync(Guid id)
        {
            var instance = await _repository.GetByIdAsync(id);
            if (instance == null)
                return null;

            var workflow = GetOrCreateWorkflow(instance);
            var status = await _provider.GetCurrentStatusAsync(workflow);

            return new WorkflowResponse
            {
                Id = instance.Id,
                ConfigurationName = instance.WorkflowDisplayName,
                WorkflowType = Enum.TryParse<WorkflowType>(instance.WorkflowType, out var parsedType) ? parsedType : default,
                CurrentState = (WorkflowState)_provider.GetCurrentState(workflow),
                Status = status,
                CreatedAt = instance.CreatedAt,
                LastUpdated = instance.LastUpdated,
                ErrorMessage = instance.ErrorMessage,
                Progress = _provider.CalculateProgress(instance, workflow),
                Metadata = instance.Metadata
            };
        }

        public async Task<IEnumerable<WorkflowResponse>> GetAllWorkflowsAsync()
        {
            var instances = await _repository.GetAllAsync();
            var responses = new List<WorkflowResponse>();

            foreach (var instance in instances)
            {
                var response = await GetWorkflowAsync(instance.Id);
                responses.Add(response);
            }

            return responses.OrderByDescending(r => r.CreatedAt);
        }

        public async Task<WorkflowResponse> StartWorkflowAsync(Guid id)
        {
            var instance = await _repository.GetByIdAsync(id);
            if (instance == null)
                throw new ArgumentException($"Workflow {id} not found");

            var workflow = GetOrCreateWorkflow(instance);

            // Add WorkflowStarted event to history (matches WorkflowService1)
            instance.History.Add(new
            {
                Timestamp = DateTime.UtcNow,
                EventType = "WorkflowStarted",
                FromState = instance.CurrentState,
                ToState = _provider.GetCurrentState(workflow),
                Description = "Workflow started",
                Data = new Dictionary<string, object>()
            });

            dynamic dynWorkflow = workflow;
            await dynWorkflow.StartAsync();

            instance.CurrentState = _provider.GetCurrentState(workflow);
            instance.Context = _provider.GetContext(workflow);
            instance.LastUpdated = DateTime.UtcNow;
            await _repository.UpdateAsync(instance);

            return await GetWorkflowAsync(id);
        }

        public async Task<WorkflowResponse> HandleExternalEventAsync(Guid id, ExternalEventRequest eventRequest)
        {
            var instance = await _repository.GetByIdAsync(id);
            if (instance == null)
                throw new ArgumentException($"Workflow {id} not found");

            var workflow = GetOrCreateWorkflow(instance);

            var handled = await _provider.HandleExternalEventAsync(workflow, eventRequest.EventType, eventRequest.Data);

            if (!handled)
                throw new ArgumentException($"Event type '{eventRequest.EventType}' not supported for workflow type {instance.WorkflowType}");

            instance.History.Add(new
            {
                Timestamp = DateTime.UtcNow,
                EventType = eventRequest.EventType,
                FromState = instance.CurrentState,
                ToState = _provider.GetCurrentState(workflow),
                Description = $"External event processed: {eventRequest.EventType}",
                Data = eventRequest.Data
            });

            instance.CurrentState = _provider.GetCurrentState(workflow);
            instance.Context = _provider.GetContext(workflow);
            instance.LastUpdated = DateTime.UtcNow;
            await _repository.UpdateAsync(instance);

            return await GetWorkflowAsync(id);
        }

        public async Task<WorkflowSummary> GetSummaryAsync()
        {
            var instances = await _repository.GetAllAsync();
            var workflows = instances.ToList();

            var summary = new WorkflowSummary
            {
                TotalWorkflows = workflows.Count,
                ActiveWorkflows = workflows.Count(w => IsActiveState(w.CurrentState)),
                CompletedWorkflows = workflows.Count(w => w.CurrentState?.ToString() == "Completed"),
                FailedWorkflows = workflows.Count(w => w.CurrentState?.ToString() == "Failed"),
                AwaitingManualAction = workflows.Count(w => RequiresManualAction(w.CurrentState))
            };

            foreach (var group in workflows.GroupBy(w => Enum.TryParse<WorkflowType>(w.WorkflowType, out var parsedType) ? parsedType : default))
                summary.ByType[group.Key] = group.Count();

            foreach (var group in workflows.GroupBy(w => Enum.TryParse<WorkflowState>(w.CurrentState?.ToString(), out var parsedState) ? parsedState : default))
                summary.ByState[group.Key] = group.Count();

            return summary;
        }

        public async Task<bool> DeleteWorkflowAsync(Guid id)
        {
            var instance = await _repository.GetByIdAsync(id);
            if (instance == null)
                return false;

            _activeWorkflows.TryRemove(id, out _);
            await _repository.DeleteAsync(id);
            return true;
        }

        public async Task<WorkflowResponse> ProceedWorkflowAsync(Guid id)
        {
            throw new NotSupportedException("Manual progression is not supported in this generic service. Please use the appropriate workflow type implementation.");
        }

        // Helper method
        private object GetOrCreateWorkflow(GenericWorkflowInstance instance)
        {
            if (instance.Context is TContext context)
            {
                return _activeWorkflows.GetOrAdd(instance.Id, _ => _provider.CreateWorkflow(context));
            }
            throw new InvalidOperationException("Instance context is not of the expected type.");
        }

        private bool IsActiveState(object state)
        {
            var stateStr = state?.ToString();
            return stateStr is "Created" or "InProgress" or "Waiting" or "ReducingTo80Percent" or
                   "WaitingAfter80Percent" or "ReducingToZero" or "Archiving" or "CreatingPR" or
                   "AwaitingReview" or "Merged" or "WaitingForDeployment" or "Transforming";
        }

        private bool RequiresManualAction(object state)
        {
            var stateStr = state?.ToString();
            return stateStr is "AwaitingReview" or "WaitingForDeployment" or "Failed";
        }
    }
}