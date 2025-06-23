using System.Collections.Concurrent;
using TechWorklowOrchestrator.ApiService.Dto;
using TechWorklowOrchestrator.ApiService.Repository;
using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Service
{
    public class WorkflowService : IWorkflowService
    {
        private readonly IWorkflowRepository _repository;
        private readonly ConcurrentDictionary<Guid, ConfigCleanupWorkflowBase> _activeWorkflows = new();

        public WorkflowService(IWorkflowRepository repository)
        {
            _repository = repository;
        }

        public async Task<Guid> CreateWorkflowAsync(CreateWorkflowRequest request)
        {
            var context = new ConfigCleanupContext
            {
                ConfigurationName = request.ConfigurationName,
                WorkflowType = request.WorkflowType,
                CurrentTrafficPercentage = request.CurrentTrafficPercentage,
                WaitDuration = request.CustomWaitDuration ?? TimeSpan.FromHours(24)
            };

            var instance = new WorkflowInstance
            {
                ConfigurationName = request.ConfigurationName,
                WorkflowType = request.WorkflowType,
                CurrentState = WorkflowState.Created,
                Context = context,
                Metadata = request.Metadata
            };

            instance.History.Add(new WorkflowEvent
            {
                EventType = "WorkflowCreated",
                FromState = WorkflowState.Created,
                ToState = WorkflowState.Created,
                Description = $"Workflow created for configuration: {request.ConfigurationName}"
            });

            var id = await _repository.CreateAsync(instance);
            return id;
        }

        public async Task<WorkflowResponse> GetWorkflowAsync(Guid id)
        {
            var instance = await _repository.GetByIdAsync(id);
            if (instance == null)
                return null;

            var workflow = GetOrCreateWorkflow(instance);
            var status = await workflow.GetCurrentStatusAsync();

            return new WorkflowResponse
            {
                Id = instance.Id,
                ConfigurationName = instance.ConfigurationName,
                WorkflowType = instance.WorkflowType,
                CurrentState = workflow.CurrentState,
                Status = status,
                CreatedAt = instance.CreatedAt,
                LastUpdated = instance.LastUpdated,
                ErrorMessage = instance.ErrorMessage,
                Progress = CalculateProgress(instance),
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

            if (workflow.CurrentState != WorkflowState.Created)
                throw new InvalidOperationException($"Workflow {id} is not in Created state");

            instance.History.Add(new WorkflowEvent
            {
                EventType = "WorkflowStarted",
                FromState = WorkflowState.Created,
                ToState = WorkflowState.InProgress,
                Description = "Workflow started"
            });

            await workflow.StartAsync();

            instance.CurrentState = workflow.CurrentState;
            instance.Context = workflow.Context;
            await _repository.UpdateAsync(instance);

            return await GetWorkflowAsync(id);
        }

        public async Task<WorkflowResponse> HandleExternalEventAsync(Guid id, ExternalEventRequest eventRequest)
        {
            var instance = await _repository.GetByIdAsync(id);
            if (instance == null)
                throw new ArgumentException($"Workflow {id} not found");

            var workflow = GetOrCreateWorkflow(instance);

            // Handle different event types
            var handled = eventRequest.EventType switch
            {
                "PRApproved" when workflow is CodeFirstWorkflow codeFirst =>
                    await HandlePRApproved(codeFirst, eventRequest.Data),
                "DeploymentCompleted" when workflow is CodeFirstWorkflow codeFirst =>
                    await HandleDeploymentCompleted(codeFirst, eventRequest.Data),
                "WaitPeriodCompleted" =>
                    await HandleWaitPeriodCompleted(workflow, eventRequest.Data),
                _ => false
            };

            if (!handled)
                throw new ArgumentException($"Event type '{eventRequest.EventType}' not supported for workflow type {instance.WorkflowType}");

            instance.History.Add(new WorkflowEvent
            {
                EventType = eventRequest.EventType,
                FromState = instance.CurrentState,
                ToState = workflow.CurrentState,
                Description = $"External event processed: {eventRequest.EventType}",
                Data = eventRequest.Data
            });

            instance.CurrentState = workflow.CurrentState;
            instance.Context = workflow.Context;
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
                CompletedWorkflows = workflows.Count(w => w.CurrentState == WorkflowState.Completed),
                FailedWorkflows = workflows.Count(w => w.CurrentState == WorkflowState.Failed),
                AwaitingManualAction = workflows.Count(w => RequiresManualAction(w.CurrentState))
            };

            // Group by type
            foreach (var group in workflows.GroupBy(w => w.WorkflowType))
            {
                summary.ByType[group.Key] = group.Count();
            }

            // Group by state
            foreach (var group in workflows.GroupBy(w => w.CurrentState))
            {
                summary.ByState[group.Key] = group.Count();
            }

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
            // This implementation depends on your existing workflow service structure
            // You'll need to adapt this based on how your WorkflowService is implemented

            throw new NotSupportedException("Manual progression is not supported in this service. Please use the appropriate workflow type implementation.");
            //// Example structure (you'll need to adapt to your actual implementation):
            //var workflow = await GetWorkflowAsync(id); // Your method to get workflow context
            //if (workflow == null)
            //    throw new ArgumentException($"Workflow {id} not found");

            //if (workflow.WorkflowType == WorkflowType.ArchiveOnly)
            //{
            //    var archiveWorkflow = new ArchiveOnlyWorkflow(workflow);

            //    if (!archiveWorkflow.CanProceed())
            //        throw new InvalidOperationException($"Workflow cannot proceed - current state: {archiveWorkflow.CurrentState}");

            //    await archiveWorkflow.ProceedAsync();

            //    // Save workflow state and return updated response
            //    await SaveWorkflowAsync(workflow); // Your method to persist changes
            //    return await GetWorkflowAsync(id); // Your method to return WorkflowResponse
            //}
            //else
            //{
            //    throw new InvalidOperationException($"Manual progression not supported for workflow type {workflow.WorkflowType}");
            //}
        }

        // Helper methods
        private ConfigCleanupWorkflowBase GetOrCreateWorkflow(WorkflowInstance instance)
        {
            return _activeWorkflows.GetOrAdd(instance.Id, _ => WorkflowFactory.CreateWorkflow(instance.Context));
        }

        private async Task<bool> HandlePRApproved(CodeFirstWorkflow workflow, Dictionary<string, object> data)
        {
            await workflow.NotifyPRApprovedAsync();
            return true;
        }

        private async Task<bool> HandleDeploymentCompleted(CodeFirstWorkflow workflow, Dictionary<string, object> data)
        {
            await workflow.NotifyDeploymentCompletedAsync();
            return true;
        }

        private async Task<bool> HandleWaitPeriodCompleted(ConfigCleanupWorkflowBase workflow, Dictionary<string, object> data)
        {
            // This would be handled by background service in real implementation
            return true;
        }

        private WorkflowProgress CalculateProgress(WorkflowInstance instance)
        {
            var (currentStep, totalSteps, description, requiresManual, manualDescription) = instance.WorkflowType switch
            {
                WorkflowType.ArchiveOnly => CalculateArchiveOnlyProgress(instance.CurrentState),
                WorkflowType.CodeFirst => CalculateCodeFirstProgress(instance.CurrentState),
                WorkflowType.TransformToDefault => CalculateTransformProgress(instance.CurrentState),
                _ => (0, 1, "Unknown", false, "")
            };

            return new WorkflowProgress
            {
                CurrentStep = currentStep,
                TotalSteps = totalSteps,
                PercentComplete = (double)currentStep / totalSteps * 100,
                CurrentStepDescription = description,
                RequiresManualAction = requiresManual,
                ManualActionDescription = manualDescription
            };
        }

        private (int current, int total, string description, bool requiresManual, string manualDesc) CalculateArchiveOnlyProgress(WorkflowState state)
        {
            return state switch
            {
                WorkflowState.Created => (0, 5, "Ready to start", false, ""),
                WorkflowState.ReducingTo80Percent => (1, 5, "Reducing traffic to 80%", false, ""),
                WorkflowState.WaitingAfter80Percent => (2, 5, "Waiting 24 hours", false, ""),
                WorkflowState.ReducingToZero => (3, 5, "Reducing traffic to 0%", false, ""),
                WorkflowState.Archiving => (4, 5, "Archiving configuration", false, ""),
                WorkflowState.Completed => (5, 5, "Completed", false, ""),
                WorkflowState.Failed => (0, 5, "Failed", true, "Review error and retry"),
                _ => (0, 5, "Unknown state", false, "")
            };
        }

        private (int current, int total, string description, bool requiresManual, string manualDesc) CalculateCodeFirstProgress(WorkflowState state)
        {
            return state switch
            {
                WorkflowState.Created => (0, 6, "Ready to create PR", false, ""),
                WorkflowState.CreatingPR => (1, 6, "Creating pull request", false, ""),
                WorkflowState.AwaitingReview => (2, 6, "Awaiting PR review", true, "Review and approve the pull request"),
                WorkflowState.Merged => (3, 6, "Merging PR", false, ""),
                WorkflowState.WaitingForDeployment => (4, 6, "Waiting for deployment", true, "Monitor deployment completion"),
                WorkflowState.Archiving => (5, 6, "Archiving configuration", false, ""),
                WorkflowState.Completed => (6, 6, "Completed", false, ""),
                WorkflowState.Failed => (0, 6, "Failed", true, "Review error and retry"),
                _ => (0, 6, "Unknown state", false, "")
            };
        }

        private (int current, int total, string description, bool requiresManual, string manualDesc) CalculateTransformProgress(WorkflowState state)
        {
            return state switch
            {
                WorkflowState.Created => (0, 2, "Ready to transform", false, ""),
                WorkflowState.Transforming => (1, 2, "Transforming to defaults", false, ""),
                WorkflowState.Completed => (2, 2, "Completed", false, ""),
                WorkflowState.Failed => (0, 2, "Failed", true, "Review error and retry"),
                _ => (0, 2, "Unknown state", false, "")
            };
        }

        private bool IsActiveState(WorkflowState state) => state switch
        {
            WorkflowState.Created or WorkflowState.InProgress or WorkflowState.Waiting or
            WorkflowState.ReducingTo80Percent or WorkflowState.WaitingAfter80Percent or
            WorkflowState.ReducingToZero or WorkflowState.Archiving or WorkflowState.CreatingPR or
            WorkflowState.AwaitingReview or WorkflowState.Merged or WorkflowState.WaitingForDeployment or
            WorkflowState.Transforming => true,
            _ => false
        };

        private bool RequiresManualAction(WorkflowState state) => state switch
        {
            WorkflowState.AwaitingReview or WorkflowState.WaitingForDeployment or WorkflowState.Failed => true,
            _ => false
        };
    }
}
