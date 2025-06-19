using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechWorklowOrchestrator.Core.Workflow
{
    // Archive-Only Workflow: 100% → 80% → wait 24h → 0% → Archive
    // TODO: include stages per file
    public class ArchiveOnlyWorkflow : ConfigCleanupWorkflowBase
    {
        public ArchiveOnlyWorkflow(ConfigCleanupContext context)
            : base(context, WorkflowState.Created)
        {
            ConfigureStateMachine();
        }

        private void ConfigureStateMachine()
        {
            _stateMachine.Configure(WorkflowState.Created)
                .Permit(WorkflowTrigger.Start, WorkflowState.ReducingTo80Percent);

            _stateMachine.Configure(WorkflowState.ReducingTo80Percent)
                .OnEntryAsync(async () => await ReduceTrafficTo80PercentAsync())
                .Permit(WorkflowTrigger.ReductionCompleted, WorkflowState.WaitingAfter80Percent)
                .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

            _stateMachine.Configure(WorkflowState.WaitingAfter80Percent)
                .OnEntryAsync(async () => await StartWaitPeriodAsync())
                .Permit(WorkflowTrigger.WaitPeriodCompleted, WorkflowState.ReducingToZero)
                .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

            _stateMachine.Configure(WorkflowState.ReducingToZero)
                .OnEntryAsync(async () => await ReduceTrafficToZeroAsync())
                .Permit(WorkflowTrigger.ReductionCompleted, WorkflowState.Archiving)
                .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

            _stateMachine.Configure(WorkflowState.Archiving)
                .OnEntryAsync(async () => await ArchiveConfigurationAsync())
                .Permit(WorkflowTrigger.ArchiveCompleted, WorkflowState.Completed)
                .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

            _stateMachine.Configure(WorkflowState.Failed)
                .OnEntry(() => Console.WriteLine($"Workflow failed: {Context.ErrorMessage}"));

            _stateMachine.Configure(WorkflowState.Completed)
                .OnEntry(() => Console.WriteLine($"Archive-only workflow completed for {Context.ConfigurationName}"));
        }

        private async Task ReduceTrafficTo80PercentAsync()
        {
            try
            {
                Console.WriteLine($"Reducing traffic to 80% for {Context.ConfigurationName}");
                // Simulate API call to reduce traffic
                await Task.Delay(200);
                Context.CurrentTrafficPercentage = 80;
                await _stateMachine.FireAsync(WorkflowTrigger.ReductionCompleted);
            }
            catch (Exception ex)
            {
                Context.ErrorMessage = ex.Message;
                await _stateMachine.FireAsync(WorkflowTrigger.Fail);
            }
        }

        private async Task StartWaitPeriodAsync()
        {
            Console.WriteLine($"Starting 24-hour wait period for {Context.ConfigurationName}");
            Context.WaitStartTime = DateTime.UtcNow;

            // In real implementation, you'd schedule this with a background service
            // For demo, we'll simulate immediate completion
            await Task.Delay(1000);
            await _stateMachine.FireAsync(WorkflowTrigger.WaitPeriodCompleted);
        }

        private async Task ReduceTrafficToZeroAsync()
        {
            try
            {
                Console.WriteLine($"Reducing traffic to 0% for {Context.ConfigurationName}");
                await Task.Delay(2000);
                Context.CurrentTrafficPercentage = 0;
                await _stateMachine.FireAsync(WorkflowTrigger.ReductionCompleted);
            }
            catch (Exception ex)
            {
                Context.ErrorMessage = ex.Message;
                await _stateMachine.FireAsync(WorkflowTrigger.Fail);
            }
        }

        private async Task ArchiveConfigurationAsync()
        {
            try
            {
                Console.WriteLine($"Archiving configuration {Context.ConfigurationName}");
                await Task.Delay(1500);
                await _stateMachine.FireAsync(WorkflowTrigger.ArchiveCompleted);
            }
            catch (Exception ex)
            {
                Context.ErrorMessage = ex.Message;
                await _stateMachine.FireAsync(WorkflowTrigger.Fail);
            }
        }

        public override async Task<bool> CanStartAsync()
        {
            // Check if configuration exists and has traffic
            return Context.CurrentTrafficPercentage > 0;
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
                WorkflowState.Created => "Ready to start traffic reduction",
                WorkflowState.ReducingTo80Percent => "Reducing traffic to 80%...",
                WorkflowState.WaitingAfter80Percent => $"Waiting (started: {Context.WaitStartTime:yyyy-MM-dd HH:mm})",
                WorkflowState.ReducingToZero => "Reducing traffic to 0%...",
                WorkflowState.Archiving => "Archiving configuration...",
                WorkflowState.Completed => "✅ Configuration archived successfully",
                WorkflowState.Failed => $"❌ Failed: {Context.ErrorMessage}",
                _ => "Unknown state"
            };
        }
    }
}
