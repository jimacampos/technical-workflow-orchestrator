using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TechWorklowOrchestrator.Web.Models;

namespace TechWorklowOrchestrator.Core.Workflow
{
    public class CodeUpdateWorkflow : BaseWorkflow<CodeUpdateContext, CodeUpdateState, CodeUpdateTrigger>
    {
        public new CodeUpdateContext Context => base.Context; // Expose as public

        public CodeUpdateWorkflow(CodeUpdateContext context)
            : base(context, CodeUpdateState.PRInProgress)
        {
            ConfigureStateMachine();
        }

        public CodeUpdateState CurrentState => _stateMachine.State;

        protected override void ConfigureStateMachine()
        {
            _stateMachine.Configure(CodeUpdateState.PRInProgress)
                .OnEntry(() =>
                {
                    Context.PRInProgressAt = DateTime.UtcNow;
                    Console.WriteLine("PR is in progress...");
                })
                .Permit(CodeUpdateTrigger.ValidateInTest, CodeUpdateState.ValidationInTestEnv)
                .Permit(CodeUpdateTrigger.Fail, CodeUpdateState.Failed);

            _stateMachine.Configure(CodeUpdateState.ValidationInTestEnv)
                .OnEntry(() =>
                {
                    Context.ValidationInTestEnvAt = DateTime.UtcNow;
                    Console.WriteLine("Validating in test environment...");
                })
                .Permit(CodeUpdateTrigger.SubmitForReview, CodeUpdateState.PRInReview)
                .Permit(CodeUpdateTrigger.Fail, CodeUpdateState.Failed);

            _stateMachine.Configure(CodeUpdateState.PRInReview)
                .OnEntry(() =>
                {
                    Context.PRInReviewAt = DateTime.UtcNow;
                    Console.WriteLine("PR is in review...");
                })
                .Permit(CodeUpdateTrigger.ApproveAndMerge, CodeUpdateState.MergedAwaitingDeployment)
                .Permit(CodeUpdateTrigger.Fail, CodeUpdateState.Failed);

            _stateMachine.Configure(CodeUpdateState.MergedAwaitingDeployment)
                .OnEntry(() =>
                {
                    Context.MergedAwaitingDeploymentAt = DateTime.UtcNow;
                    Console.WriteLine("PR merged, awaiting deployment...");
                })
                .Permit(CodeUpdateTrigger.DetectDeployment, CodeUpdateState.DeploymentDone)
                .Permit(CodeUpdateTrigger.Fail, CodeUpdateState.Failed);

            _stateMachine.Configure(CodeUpdateState.DeploymentDone)
                .OnEntry(() =>
                {
                    Context.DeploymentDoneAt = DateTime.UtcNow;
                    Context.IsCompleted = true;
                    Context.CompletedAt = DateTime.UtcNow;
                    Console.WriteLine("✅ Deployment done.");
                });

            _stateMachine.Configure(CodeUpdateState.Failed)
                .OnEntry(() =>
                {
                    Console.WriteLine($"❌ Workflow failed: {Context.ErrorMessage}");
                });
        }

        public async Task ValidateInTestEnvAsync()
        {
            if (CanFire(CodeUpdateTrigger.ValidateInTest))
                await FireAsync(CodeUpdateTrigger.ValidateInTest);
        }

        public async Task SubmitForReviewAsync()
        {
            if (CanFire(CodeUpdateTrigger.SubmitForReview))
                await FireAsync(CodeUpdateTrigger.SubmitForReview);
        }

        public async Task ApproveAndMergeAsync()
        {
            if (CanFire(CodeUpdateTrigger.ApproveAndMerge))
                await FireAsync(CodeUpdateTrigger.ApproveAndMerge);
        }

        public async Task DetectDeploymentAsync()
        {
            if (CanFire(CodeUpdateTrigger.DetectDeployment))
                await FireAsync(CodeUpdateTrigger.DetectDeployment);
        }

        public override async Task<bool> CanStartAsync()
        {
            // Require a title to start instead of UpdateName
            return !string.IsNullOrEmpty(Context.Title);
        }

        public override async Task StartAsync()
        {
            if (await CanStartAsync())
            {
                Context.StartedAt = DateTime.UtcNow;
                // Optionally fire a start trigger if you add one
            }
        }

        public override async Task<string> GetCurrentStatusAsync()
        {
            return Context.GetStatusDescription();
        }

        public override List<string> GetAvailableActions()
        {
            return CurrentState switch
            {
                CodeUpdateState.PRInProgress => new List<string> { "Validate In Test Env" },
                CodeUpdateState.ValidationInTestEnv => new List<string> { "Submit For Review" },
                CodeUpdateState.PRInReview => new List<string> { "Approve And Merge" },
                CodeUpdateState.MergedAwaitingDeployment => new List<string> { "Detect Deployment" },
                CodeUpdateState.DeploymentDone => new List<string>(),
                CodeUpdateState.Failed => new List<string>(),
                _ => new List<string>()
            };
        }

        public Task<bool> HandleExternalEventAsync(string eventType, Dictionary<string, object> data)
        {
            // Implement your event handling logic here
            return Task.FromResult(false);
        }
    }
}