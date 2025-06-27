using TechWorklowOrchestrator.ApiService.Dto;
using TechWorklowOrchestrator.ApiService.Repository;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Service
{
    public class CodeUpdateWorkflowProvider : IWorkflowProvider<CodeUpdateWorkflowRequest, CodeUpdateContext>
    {
        public CodeUpdateContext CreateContext(CodeUpdateWorkflowRequest request)
        {
            return new CodeUpdateContext
            {
                Title = request.Title,
                Description = request.Description,
                ProjectId = request.ProjectId,
                Metadata = request.Metadata
            };
        }

        public object CreateWorkflow(CodeUpdateContext context)
        {
            return new CodeUpdateWorkflow(context);
        }

        public async Task<bool> HandleExternalEventAsync(object workflow, string eventType, Dictionary<string, object> data)
        {
            if (workflow is not CodeUpdateWorkflow codeUpdateWorkflow)
                throw new ArgumentException("Invalid workflow type for CodeUpdateWorkflow");

            // If not implemented, return false or throw NotSupportedException
            if (codeUpdateWorkflow.GetType().GetMethod("HandleExternalEventAsync") == null)
                return false;

            // You may need to implement this method in CodeUpdateWorkflow
            return await codeUpdateWorkflow.HandleExternalEventAsync(eventType, data);
        }

        public async Task<string> GetCurrentStatusAsync(object workflow)
        {
            if (workflow is not CodeUpdateWorkflow codeUpdateWorkflow)
                throw new ArgumentException("Invalid workflow type for CodeUpdateWorkflow");

            // Use the context's status description for a user-friendly status
            return codeUpdateWorkflow.Context?.GetStatusDescription() ?? "Unknown";
        }

        public object GetCurrentState(object workflow)
        {
            if (workflow is not CodeUpdateWorkflow codeUpdateWorkflow)
                throw new ArgumentException("Invalid workflow type for CodeUpdateWorkflow");

            return codeUpdateWorkflow.CurrentState;
        }

        public object GetContext(object workflow)
        {
            if (workflow is not CodeUpdateWorkflow codeUpdateWorkflow)
                throw new ArgumentException("Invalid workflow type for CodeUpdateWorkflow");

            return codeUpdateWorkflow.Context;
        }

        public WorkflowProgress CalculateProgress(GenericWorkflowInstance instance, object workflow)
        {
            if (instance.CurrentState is not CodeUpdateState codeUpdateState)
                throw new ArgumentException("Invalid state type for CodeUpdateWorkflow");

            var (currentStep, totalSteps, description, requiresManual, manualDescription) = CalculateCodeUpdateProgress(codeUpdateState);

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

        private (int current, int total, string description, bool requiresManual, string manualDesc) CalculateCodeUpdateProgress(CodeUpdateState state)
        {
            return state switch
            {
                CodeUpdateState.PRInProgress => (0, 6, "Pull request in progress", false, ""),
                CodeUpdateState.ValidationInTestEnv => (1, 6, "Validating in test environment", false, ""),
                CodeUpdateState.PRInReview => (2, 6, "Pull request in review", true, "Review and approve the pull request"),
                CodeUpdateState.MergedAwaitingDeployment => (3, 6, "Merged, awaiting deployment", true, "Monitor deployment completion"),
                CodeUpdateState.DeploymentDone => (4, 6, "Deployment completed", false, ""),
                CodeUpdateState.Failed => (0, 6, "Failed", true, "Review error and retry"),
                _ => (0, 6, "Unknown state", false, "")
            };
        }

        public string GetDisplayName(object context)
        {
            if (context is not CodeUpdateContext codeUpdateContext)
                throw new ArgumentException("Invalid context type for CodeUpdateWorkflow");

            return codeUpdateContext.Title;
        }

        public Dictionary<string,string> GetMetaData(object context)
        {
            if (context is not CodeUpdateWorkflowRequest request)
                throw new ArgumentException("Invalid context type for CodeUpdateWorkflow");

            return request.Metadata;
        }

        public string GetWorkflowType(object context)
        {
            if (context is not CodeUpdateContext)
                throw new ArgumentException("Invalid context type for CodeUpdateWorkflow");

            return "CodeUpdate";
        }
    }
}