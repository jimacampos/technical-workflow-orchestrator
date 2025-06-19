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
            : base(context, WorkflowState.Created)
        {
            ConfigureStateMachine();
        }

        private void ConfigureStateMachine()
        {
            _stateMachine.Configure(WorkflowState.Created)
                .Permit(WorkflowTrigger.Start, WorkflowState.Transforming);

            _stateMachine.Configure(WorkflowState.Transforming)
                .OnEntryAsync(async () => await TransformConfigurationAsync())
                .Permit(WorkflowTrigger.TransformCompleted, WorkflowState.Completed)
                .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

            _stateMachine.Configure(WorkflowState.Completed)
                .OnEntry(() => Console.WriteLine($"Transform-to-default completed for {Context.ConfigurationName}"));
        }

        private async Task TransformConfigurationAsync()
        {
            try
            {
                Console.WriteLine($"Transforming {Context.ConfigurationName} to default values");
                await Task.Delay(2000); // Simulate transformation
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
            // Check if configuration can be transformed
            return !string.IsNullOrEmpty(Context.ConfigurationName);
        }

        public override async Task StartAsync()
        {
            if (await CanStartAsync())
            {
                await _stateMachine.FireAsync(WorkflowTrigger.Start);
            }
        }

        public override async Task<string> GetCurrentStatusAsync()
        {
            return CurrentState switch
            {
                WorkflowState.Created => "Ready to transform",
                WorkflowState.Transforming => "Transforming to default values...",
                WorkflowState.Completed => "✅ Configuration transformed to defaults",
                WorkflowState.Failed => $"❌ Failed: {Context.ErrorMessage}",
                _ => "Unknown state"
            };
        }
    }

}
