using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TechWorklowOrchestrator.ApiService.Dto;
using TechWorklowOrchestrator.ApiService.Repository;
using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Service
{
    public class ConfigCleanupWorkflowProvider : IWorkflowProvider<CreateWorkflowRequest, ConfigCleanupContext>
    {
        public ConfigCleanupContext CreateContext(CreateWorkflowRequest request)
        {
            return new ConfigCleanupContext
            {
                ConfigurationName = request.ConfigurationName,
                WorkflowType = request.WorkflowType,
                CurrentTrafficPercentage = request.CurrentTrafficPercentage,
                WaitDuration = request.CustomWaitDuration ?? TimeSpan.FromHours(24),
            };
        }

        public object CreateWorkflow(ConfigCleanupContext context)
        {
            return WorkflowFactory.CreateWorkflow(context);
        }

        public async Task<bool> HandleExternalEventAsync(object workflow, string eventType, Dictionary<string, object> data)
        {
            if (workflow is CodeFirstWorkflow codeFirst)
            {
                switch (eventType)
                {
                    case "PRApproved":
                        await codeFirst.NotifyPRApprovedAsync();
                        return true;
                    case "DeploymentCompleted":
                        await codeFirst.NotifyDeploymentCompletedAsync();
                        return true;
                }
            }
            if (workflow is ConfigCleanupWorkflowBase cleanup)
            {
                if (eventType == "WaitPeriodCompleted")
                {
                    // This would be handled by background service in real implementation
                    return true;
                }
            }
            return false;
        }

        public async Task<string> GetCurrentStatusAsync(object workflow)
        {
            if (workflow is ConfigCleanupWorkflowBase cleanup)
                return await cleanup.GetCurrentStatusAsync();
            return "Unknown";
        }

        public object GetCurrentState(object workflow)
        {
            if (workflow is ConfigCleanupWorkflowBase cleanup)
                return cleanup.CurrentState;
            return null;
        }

        public object GetContext(object workflow)
        {
            if (workflow is ConfigCleanupWorkflowBase cleanup)
                return cleanup.Context;
            return null;
        }

        public WorkflowProgress CalculateProgress(GenericWorkflowInstance instance, object workflow)
        {
            var state = instance.CurrentState is WorkflowState ws ? ws : WorkflowState.Created;
            var type = Enum.TryParse<WorkflowType>(instance.WorkflowType, out var t) ? t : WorkflowType.CodeFirst;

            (int current, int total, string desc, bool manual, string manualDesc) progress = type switch
            {
                WorkflowType.ArchiveOnly => CalculateArchiveOnlyProgress(state),
                WorkflowType.CodeFirst => CalculateCodeFirstProgress(state),
                WorkflowType.TransformToDefault => CalculateTransformProgress(state),
                _ => (0, 1, "Unknown", false, "")
            };

            return new WorkflowProgress
            {
                CurrentStep = progress.current,
                TotalSteps = progress.total,
                PercentComplete = (double)progress.current / progress.total * 100,
                CurrentStepDescription = progress.desc,
                RequiresManualAction = progress.manual,
                ManualActionDescription = progress.manualDesc
            };
        }

        public string GetDisplayName(object context)
        {
            if (context is ConfigCleanupContext cleanup)
                return cleanup.ConfigurationName;
            if (context is CreateWorkflowRequest req)
                return req.ConfigurationName;
            return "Unknown";
        }

        public string GetWorkflowType(object context) //ConfigCleanupContext
        {
            if (context is ConfigCleanupContext cleanup)
                return cleanup.WorkflowType.ToString();
            if (context is CreateWorkflowRequest req)
                return req.WorkflowType.ToString();
            return "Unknown";
        }

        public Dictionary<string, string> GetMetaData(object context)
        {
            if (context is not CreateWorkflowRequest request)
                throw new ArgumentException("Invalid context type for CreateWorkflowRequest");

            return request.Metadata;
        }

        // Progress calculation helpers
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
    }
}