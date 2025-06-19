using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechWorklowOrchestrator.Core.Workflow
{
    // Code-First Workflow: Create PR → Review → Merge → Wait for deployment → Archive
    // TODO: add option to transform workflow to archive when it is finished
    public class CodeFirstWorkflow : ConfigCleanupWorkflowBase
    {
        public CodeFirstWorkflow(ConfigCleanupContext context)
            : base(context, WorkflowState.Created)
        {
            ConfigureStateMachine();
        }

        private void ConfigureStateMachine()
        {
            _stateMachine.Configure(WorkflowState.Created)
                .Permit(WorkflowTrigger.Start, WorkflowState.CreatingPR);

            _stateMachine.Configure(WorkflowState.CreatingPR)
                .OnEntryAsync(async () => await CreatePullRequestAsync())
                .Permit(WorkflowTrigger.PRCreated, WorkflowState.AwaitingReview)
                .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

            _stateMachine.Configure(WorkflowState.AwaitingReview)
                .OnEntry(() => Console.WriteLine($"PR created, awaiting review: {Context.PullRequestUrl}"))
                .Permit(WorkflowTrigger.PRApproved, WorkflowState.Merging)
                .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

            _stateMachine.Configure(WorkflowState.Merging)
                .OnEntryAsync(async () => await MergePullRequestAsync())
                .Permit(WorkflowTrigger.PRMerged, WorkflowState.WaitingForDeployment)
                .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

            _stateMachine.Configure(WorkflowState.WaitingForDeployment)
                .OnEntry(() => Console.WriteLine("Waiting for deployment to complete..."))
                .Permit(WorkflowTrigger.DeploymentDetected, WorkflowState.Archiving)
                .Permit(WorkflowTrigger.Timeout, WorkflowState.Failed);

            _stateMachine.Configure(WorkflowState.Archiving)
                .OnEntryAsync(async () => await ArchiveConfigurationAsync())
                .Permit(WorkflowTrigger.ArchiveCompleted, WorkflowState.Completed)
                .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

            _stateMachine.Configure(WorkflowState.Completed)
                .OnEntry(() => Console.WriteLine($"Code-first workflow completed for {Context.ConfigurationName}"));
        }

        private async Task CreatePullRequestAsync()
        {
            try
            {
                Console.WriteLine($"Creating PR to remove {Context.ConfigurationName} from code");
                await Task.Delay(3000); // Simulate PR creation
                Context.PullRequestUrl = $"https://github.com/company/repo/pull/12345";
                await _stateMachine.FireAsync(WorkflowTrigger.PRCreated);
            }
            catch (Exception ex)
            {
                Context.ErrorMessage = ex.Message;
                await _stateMachine.FireAsync(WorkflowTrigger.Fail);
            }
        }

        private async Task MergePullRequestAsync()
        {
            try
            {
                Console.WriteLine("Merging approved PR...");
                await Task.Delay(1000);
                await _stateMachine.FireAsync(WorkflowTrigger.PRMerged);
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

        // Manual trigger methods for external events
        public async Task NotifyPRApprovedAsync()
        {
            if (_stateMachine.CanFire(WorkflowTrigger.PRApproved))
            {
                await _stateMachine.FireAsync(WorkflowTrigger.PRApproved);
            }
        }

        public async Task NotifyDeploymentCompletedAsync()
        {
            if (_stateMachine.CanFire(WorkflowTrigger.DeploymentDetected))
            {
                await _stateMachine.FireAsync(WorkflowTrigger.DeploymentDetected);
            }
        }

        public override async Task<bool> CanStartAsync()
        {
            // Check if configuration exists in code
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
                WorkflowState.Created => "Ready to create PR",
                WorkflowState.CreatingPR => "Creating pull request...",
                WorkflowState.AwaitingReview => $"⏳ Awaiting PR review: {Context.PullRequestUrl}",
                WorkflowState.Merging => "Merging approved PR...",
                WorkflowState.WaitingForDeployment => "⏳ Waiting for deployment",
                WorkflowState.Archiving => "Archiving configuration...",
                WorkflowState.Completed => "✅ Code removed and configuration archived",
                WorkflowState.Failed => $"❌ Failed: {Context.ErrorMessage}",
                _ => "Unknown state"
            };
        }
    }
}
