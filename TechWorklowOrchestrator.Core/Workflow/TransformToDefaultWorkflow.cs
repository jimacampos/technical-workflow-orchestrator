using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechWorklowOrchestrator.Core.Workflow
{
    // Transform-to-Default Workflow: Single immediate action
    public class TransformToDefaultWorkflow : ConfigCleanupWorkflowBase
    {
        public TransformToDefaultWorkflow(ConfigCleanupContext context)
            : base(context, DetermineInitialState(context))
        {
            ConfigureStateMachine();
        }

        private void ConfigureStateMachine()
        {
            // Created -> AwaitingUserAction (ready to transform)
            _stateMachine.Configure(WorkflowState.Created)
                .OnEntry(() => Console.WriteLine("Transform workflow created, ready to start"))
                .Permit(WorkflowTrigger.Start, WorkflowState.AwaitingUserAction);

            // AwaitingUserAction -> Transforming (user clicks to start)
            _stateMachine.Configure(WorkflowState.AwaitingUserAction)
                .OnEntry(() => Console.WriteLine("Transform workflow awaiting user action"))
                .Permit(WorkflowTrigger.UserProceed, WorkflowState.Transforming)
                .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

            // Transforming -> Completed or Failed
            _stateMachine.Configure(WorkflowState.Transforming)
                .OnEntry(() => Console.WriteLine("Starting transformation..."))
                .OnEntryAsync(async () => await TransformConfigurationAsync())
                .Permit(WorkflowTrigger.TransformCompleted, WorkflowState.Completed)
                .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

            // Terminal states
            _stateMachine.Configure(WorkflowState.Completed)
                .OnEntry(() => {
                    Context.IsCompleted = true;
                    Console.WriteLine($"Transform-to-default completed for {Context.ConfigurationName}");
                });

            _stateMachine.Configure(WorkflowState.Failed)
                .OnEntry(() => Console.WriteLine($"Transform-to-default failed for {Context.ConfigurationName}: {Context.ErrorMessage}"));
        }

        private async Task TransformConfigurationAsync()
        {
            try
            {
                // Simulate transformation work
                await Task.Delay(2000); // Simulate API call

                await _stateMachine.FireAsync(WorkflowTrigger.TransformCompleted);
            }
            catch (Exception ex)
            {
                Context.ErrorMessage = ex.Message;
                await _stateMachine.FireAsync(WorkflowTrigger.Fail);
            }
        }

        public override async Task<bool> CanStartAsync()
        {

            var canStart = !string.IsNullOrEmpty(Context.ConfigurationName) &&
                          !Context.IsCompleted &&
                          string.IsNullOrEmpty(Context.ErrorMessage);

            return canStart;
        }

        public override async Task StartAsync()
        {
            if (await CanStartAsync())
            {
                var startTime = DateTime.UtcNow;
                Context.TransformStartedAt = startTime;

                await _stateMachine.FireAsync(WorkflowTrigger.Start);
            }
            else
            {
                throw new InvalidOperationException("Cannot start TransformToDefault workflow - check configuration");
            }
        }

        public override async Task<string> GetCurrentStatusAsync()
        {
            return CurrentState switch
            {
                WorkflowState.Created => "Ready to transform",
                WorkflowState.AwaitingUserAction => "⏳ Ready to transform to default values",
                WorkflowState.Transforming => "🔄 Transforming to default values...",
                WorkflowState.Completed => "✅ Configuration transformed to defaults",
                WorkflowState.Failed => $"❌ Failed: {Context.ErrorMessage}",
                _ => $"Unknown state: {CurrentState}"
            };
        }

        // Methods for consistency with ArchiveOnlyWorkflow interface
        public bool CanProceed()
        {
            var canProceed = CurrentState == WorkflowState.AwaitingUserAction;
            
            return canProceed;
        }

        public async Task ProceedAsync()
        {

            if (CurrentState != WorkflowState.AwaitingUserAction)
            {
                var message = $"Cannot proceed: workflow is in {CurrentState} state, expected AwaitingUserAction";
                throw new InvalidOperationException(message);
            }

            await _stateMachine.FireAsync(WorkflowTrigger.UserProceed);
        }

        public string GetCurrentActionDescription()
        {
            return CurrentState switch
            {
                WorkflowState.Created => $"Ready to transform '{Context.ConfigurationName}' to default values",
                WorkflowState.AwaitingUserAction => $"Click to transform '{Context.ConfigurationName}' to default values",
                WorkflowState.Transforming => "Transformation in progress...",
                WorkflowState.Completed => "Transformation completed",
                WorkflowState.Failed => $"Transformation failed: {Context.ErrorMessage}",
                _ => $"Unknown state: {CurrentState}"
            };
        }

        public string GetProceedButtonText()
        {
            return CurrentState switch
            {
                WorkflowState.AwaitingUserAction => "Transform Now",
                _ => "Proceed"
            };
        }

        private static WorkflowState DetermineInitialState(ConfigCleanupContext context)
        {
            if (context.IsCompleted)
            {
                return WorkflowState.Completed;
            }

            if (!string.IsNullOrEmpty(context.ErrorMessage))
            {
                return WorkflowState.Failed;
            }

            // Check if workflow has been started - THIS WAS MISSING!
            if (context.TransformStartedAt.HasValue)
            {
                return WorkflowState.AwaitingUserAction;
            }

            return WorkflowState.Created;
        }
    }
}