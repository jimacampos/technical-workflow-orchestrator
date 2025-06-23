using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechWorklowOrchestrator.Core.Workflow
{
    // Code-First Workflow: Create PR → Review → Merge → Wait for deployment → Complete
    // Manual workflow - user triggers each step, automation can be added later
    public class CodeFirstWorkflow : ConfigCleanupWorkflowBase
    {
        public CodeFirstWorkflow(ConfigCleanupContext context)
            : base(context, WorkflowState.Created)
        {
            ConfigureStateMachine();
        }

        private void ConfigureStateMachine()
        {
            // 1. Created → Ready to create code changes
            _stateMachine.Configure(WorkflowState.Created)
                .OnEntry(() => Console.WriteLine($"CodeFirst workflow created for {Context.ConfigurationName}"))
                .Permit(WorkflowTrigger.Start, WorkflowState.InProgress);

            // 2. InProgress → Currently working on the code changes  
            _stateMachine.Configure(WorkflowState.InProgress)
                .OnEntry(() => Console.WriteLine("Working on code changes..."))
                .Permit(WorkflowTrigger.UserProceed, WorkflowState.CreatingPR)
                .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

            // 3. CreatingPR → User provides PR link
            _stateMachine.Configure(WorkflowState.CreatingPR)
                .OnEntry(() => Console.WriteLine("Ready to create/link pull request"))
                .Permit(WorkflowTrigger.PRCreated, WorkflowState.AwaitingReview)
                .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

            // 4. AwaitingReview → PR created, waiting for review/approval
            _stateMachine.Configure(WorkflowState.AwaitingReview)
                .OnEntry(() => Console.WriteLine($"PR awaiting review: {Context.PullRequestUrl}"))
                .Permit(WorkflowTrigger.PRApproved, WorkflowState.Merged)
                .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

            // 5. Merged → PR approved and merged to main branch
            _stateMachine.Configure(WorkflowState.Merged)
                .OnEntry(() => Console.WriteLine("PR has been merged to main branch"))
                .Permit(WorkflowTrigger.PRMerged, WorkflowState.WaitingForDeployment)
                .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

            // 6. WaitingForDeployment → Code merged, waiting for deployment detection
            _stateMachine.Configure(WorkflowState.WaitingForDeployment)
                .OnEntry(() => Console.WriteLine("Waiting for deployment to complete..."))
                .Permit(WorkflowTrigger.DeploymentDetected, WorkflowState.Completed)
                .Permit(WorkflowTrigger.Timeout, WorkflowState.Failed);

            // 7. Completed → Everything done
            _stateMachine.Configure(WorkflowState.Completed)
                .OnEntry(() => Console.WriteLine($"✅ CodeFirst workflow completed for {Context.ConfigurationName}"));

            // Error state
            _stateMachine.Configure(WorkflowState.Failed)
                .OnEntry(() => Console.WriteLine($"❌ Workflow failed: {Context.ErrorMessage}"));
        }

        // Manual trigger methods for user actions
        public async Task StartCodeWorkAsync()
        {
            if (_stateMachine.CanFire(WorkflowTrigger.Start))
            {
                Context.CodeWorkStartedAt = DateTime.UtcNow;
                await _stateMachine.FireAsync(WorkflowTrigger.Start);
            }
        }

        public async Task CompleteCodeWorkAsync()
        {
            if (_stateMachine.CanFire(WorkflowTrigger.UserProceed))
            {
                await _stateMachine.FireAsync(WorkflowTrigger.UserProceed);
            }
        }

        public async Task SetPullRequestAsync(string prUrl)
        {
            if (_stateMachine.CanFire(WorkflowTrigger.PRCreated))
            {
                Context.PullRequestUrl = prUrl;
                await _stateMachine.FireAsync(WorkflowTrigger.PRCreated);
            }
        }

        public async Task NotifyPRApprovedAsync()
        {
            if (_stateMachine.CanFire(WorkflowTrigger.PRApproved))
            {
                await _stateMachine.FireAsync(WorkflowTrigger.PRApproved);
            }
        }

        public async Task ConfirmMergeAsync()
        {
            if (_stateMachine.CanFire(WorkflowTrigger.PRMerged))
            {
                await _stateMachine.FireAsync(WorkflowTrigger.PRMerged);
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
            // Check if configuration exists and is valid for code-first cleanup
            return !string.IsNullOrEmpty(Context.ConfigurationName);
        }

        public override async Task StartAsync()
        {
            if (await CanStartAsync())
            {
                await StartCodeWorkAsync();
            }
        }

        public override async Task<string> GetCurrentStatusAsync()
        {
            return CurrentState switch
            {
                WorkflowState.Created => "Ready to start code changes",
                WorkflowState.InProgress => "🔄 Working on code changes",
                WorkflowState.CreatingPR => "Ready to create/link pull request",
                WorkflowState.AwaitingReview => $"⏳ Awaiting PR review: {Context.PullRequestUrl}",
                WorkflowState.Merged => "🔄 PR merged, ready to confirm deployment wait",
                WorkflowState.WaitingForDeployment => "⏳ Waiting for deployment",
                WorkflowState.Completed => "✅ Code removed and deployed",
                WorkflowState.Failed => $"❌ Failed: {Context.ErrorMessage}",
                _ => "Unknown state"
            };
        }

        // Helper method to get available actions for current state
        public List<string> GetAvailableActions()
        {
            return CurrentState switch
            {
                WorkflowState.Created => new List<string> { "Start Code Work" },
                WorkflowState.InProgress => new List<string> { "Complete Code Work" },
                WorkflowState.CreatingPR => new List<string> { "Set Pull Request URL" },
                WorkflowState.AwaitingReview => new List<string> { "Mark PR as Approved" },
                WorkflowState.Merged => new List<string> { "Confirm Merge" },
                WorkflowState.WaitingForDeployment => new List<string> { "Confirm Deployment" },
                WorkflowState.Completed => new List<string>(),
                WorkflowState.Failed => new List<string>(),
                _ => new List<string>()
            };
        }
    }
}