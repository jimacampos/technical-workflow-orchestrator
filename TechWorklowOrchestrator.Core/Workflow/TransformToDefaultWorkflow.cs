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
            Console.WriteLine($"=== TransformToDefaultWorkflow Constructor Debug ===");
            Console.WriteLine($"ConfigurationName: '{Context.ConfigurationName}'");
            Console.WriteLine($"IsCompleted: {Context.IsCompleted}");
            Console.WriteLine($"ErrorMessage: '{Context.ErrorMessage}'");
            Console.WriteLine($"Determined Initial State: {CurrentState}");
            Console.WriteLine($"=== End Constructor Debug ===");
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
                Console.WriteLine($"Transforming {Context.ConfigurationName} to default values");

                // Simulate transformation work
                await Task.Delay(2000); // Simulate API call

                Console.WriteLine($"Transformation completed successfully");
                await _stateMachine.FireAsync(WorkflowTrigger.TransformCompleted);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during transformation: {ex.Message}");
                Context.ErrorMessage = ex.Message;
                await _stateMachine.FireAsync(WorkflowTrigger.Fail);
            }
        }

        public override async Task<bool> CanStartAsync()
        {
            Console.WriteLine($"=== CanStartAsync Debug ===");
            Console.WriteLine($"ConfigurationName: '{Context.ConfigurationName}'");
            Console.WriteLine($"IsEmpty: {string.IsNullOrEmpty(Context.ConfigurationName)}");
            Console.WriteLine($"IsCompleted: {Context.IsCompleted}");
            Console.WriteLine($"ErrorMessage: '{Context.ErrorMessage}'");
            Console.WriteLine($"ErrorIsEmpty: {string.IsNullOrEmpty(Context.ErrorMessage)}");
            Console.WriteLine($"CurrentState: {CurrentState}");

            var canStart = !string.IsNullOrEmpty(Context.ConfigurationName) &&
                          !Context.IsCompleted &&
                          string.IsNullOrEmpty(Context.ErrorMessage);

            Console.WriteLine($"CanStartAsync result: {canStart}");
            Console.WriteLine($"=== End CanStartAsync Debug ===");
            return canStart;
        }

        public override async Task StartAsync()
        {
            Console.WriteLine($"=== StartAsync Debug ===");
            Console.WriteLine($"Current state before start: {CurrentState}");

            if (await CanStartAsync())
            {
                Console.WriteLine($"Starting TransformToDefault workflow for {Context.ConfigurationName}");

                var startTime = DateTime.UtcNow;
                Console.WriteLine($"Setting TransformStartedAt to: {startTime}");
                Context.TransformStartedAt = startTime;
                Console.WriteLine($"TransformStartedAt after setting: {Context.TransformStartedAt}");

                Console.WriteLine($"Firing Start trigger...");
                await _stateMachine.FireAsync(WorkflowTrigger.Start);
                Console.WriteLine($"Start trigger fired, new state: {CurrentState}");
            }
            else
            {
                Console.WriteLine("CanStartAsync returned false - cannot start workflow");
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
            Console.WriteLine($"=== CanProceed Debug ===");
            Console.WriteLine($"CurrentState: {CurrentState}");
            Console.WriteLine($"Expected state: {WorkflowState.AwaitingUserAction}");
            Console.WriteLine($"States match: {CurrentState == WorkflowState.AwaitingUserAction}");

            var canProceed = CurrentState == WorkflowState.AwaitingUserAction;
            Console.WriteLine($"CanProceed result: {canProceed}");
            Console.WriteLine($"=== End CanProceed Debug ===");

            return canProceed;
        }

        public async Task ProceedAsync()
        {
            Console.WriteLine($"=== ProceedAsync Debug ===");
            Console.WriteLine($"Current state: {CurrentState}");

            if (CurrentState != WorkflowState.AwaitingUserAction)
            {
                var message = $"Cannot proceed: workflow is in {CurrentState} state, expected AwaitingUserAction";
                Console.WriteLine(message);
                throw new InvalidOperationException(message);
            }

            Console.WriteLine($"User proceeding with transformation of {Context.ConfigurationName}");
            Console.WriteLine($"Firing UserProceed trigger...");
            await _stateMachine.FireAsync(WorkflowTrigger.UserProceed);
            Console.WriteLine($"UserProceed trigger fired, new state: {CurrentState}");
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
            Console.WriteLine($"=== DetermineInitialState Debug ===");
            Console.WriteLine($"IsCompleted: {context.IsCompleted}");
            Console.WriteLine($"ErrorMessage: '{context.ErrorMessage}'");
            Console.WriteLine($"HasError: {!string.IsNullOrEmpty(context.ErrorMessage)}");
            Console.WriteLine($"TransformStartedAt: {context.TransformStartedAt}");
            Console.WriteLine($"HasBeenStarted: {context.TransformStartedAt.HasValue}");

            if (context.IsCompleted)
            {
                Console.WriteLine("Returning: Completed");
                return WorkflowState.Completed;
            }

            if (!string.IsNullOrEmpty(context.ErrorMessage))
            {
                Console.WriteLine("Returning: Failed");
                return WorkflowState.Failed;
            }

            // Check if workflow has been started - THIS WAS MISSING!
            if (context.TransformStartedAt.HasValue)
            {
                Console.WriteLine("Returning: AwaitingUserAction (already started)");
                return WorkflowState.AwaitingUserAction;
            }

            // For TransformToDefault, if it's not completed, has no error, and hasn't been started
            Console.WriteLine("Returning: Created");
            Console.WriteLine($"=== End DetermineInitialState Debug ===");
            return WorkflowState.Created;
        }
    }
}