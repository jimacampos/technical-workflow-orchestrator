using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechWorklowOrchestrator.Core.Workflow
{
    // Enhanced Archive-Only Workflow with two-phase stages (reduction → wait → final reduction → wait → next stage)
    public class ArchiveOnlyWorkflow : ConfigCleanupWorkflowBase
    {
        public ArchiveOnlyWorkflow(ConfigCleanupContext context)
            : base(context, WorkflowState.Created)
        {
            // Initialize archive configuration if not already set
            if (context.ArchiveConfiguration == null)
            {
                // Default configuration for backward compatibility
                context.InitializeArchiveConfiguration(new List<(string, int, int, TimeSpan?)>
                {
                    ("Production", 100, 0, TimeSpan.FromHours(24)),
                    ("Test", 100, 0, TimeSpan.FromHours(4)),
                    ("Development", 100, 0, TimeSpan.FromHours(1))
                });
            }

            ConfigureStateMachine();
        }

        private void ConfigureStateMachine()
        {
            _stateMachine.Configure(WorkflowState.Created)
                .Permit(WorkflowTrigger.Start, WorkflowState.InProgress);

            _stateMachine.Configure(WorkflowState.InProgress)
                .OnEntryAsync(async () => await ProcessCurrentStageReductionAsync())
                .Permit(WorkflowTrigger.ReductionCompleted, WorkflowState.Waiting)
                .Permit(WorkflowTrigger.Complete, WorkflowState.Completed)
                .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

            _stateMachine.Configure(WorkflowState.Waiting)
                .OnEntryAsync(async () => await StartWaitPeriodAsync())
                .Permit(WorkflowTrigger.WaitPeriodCompleted, WorkflowState.InProgress)
                .Permit(WorkflowTrigger.Complete, WorkflowState.Completed)
                .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

            _stateMachine.Configure(WorkflowState.Failed)
                .OnEntry(() => Console.WriteLine($"Workflow failed: {Context.ErrorMessage}"));

            _stateMachine.Configure(WorkflowState.Completed)
                .OnEntry(() =>
                {
                    Context.IsCompleted = true;
                    Context.ArchiveConfiguration!.WorkflowCompletedAt = DateTime.UtcNow;
                    Console.WriteLine($"Archive workflow completed for {Context.ConfigurationName}");
                });
        }

        private async Task ProcessCurrentStageReductionAsync()
        {
            try
            {
                var config = Context.ArchiveConfiguration!;
                var currentStage = config.CurrentStage;

                if (currentStage == null)
                {
                    // All stages completed - finish workflow
                    Console.WriteLine($"All stages completed for {Context.ConfigurationName}");
                    await _stateMachine.FireAsync(WorkflowTrigger.Complete);
                    return;
                }

                // Determine what phase we're in for this stage
                var needsInitialReduction = currentStage.Status == WorkflowStageStatus.Pending;
                var needsFinalReduction = currentStage.Status == WorkflowStageStatus.Waiting &&
                                         currentStage.CurrentAllocationPercentage > currentStage.TargetAllocationPercentage;

                if (needsInitialReduction)
                {
                    // Phase 1: Initial reduction (e.g., 100% → 80%)
                    await ProcessInitialReductionAsync(currentStage);
                }
                else if (needsFinalReduction)
                {
                    // Phase 2: Final reduction after wait (e.g., 80% → 0%)
                    await ProcessFinalReductionAsync(currentStage);
                }
                else
                {
                    // Stage is completely done, move to next stage
                    await MoveToNextStageAsync();
                }
            }
            catch (Exception ex)
            {
                var currentStage = Context.ArchiveConfiguration?.CurrentStage;
                if (currentStage != null)
                {
                    currentStage.Status = WorkflowStageStatus.Failed;
                    currentStage.ErrorMessage = ex.Message;
                }
                Context.ErrorMessage = ex.Message;
                await _stateMachine.FireAsync(WorkflowTrigger.Fail);
            }
        }

        private async Task ProcessInitialReductionAsync(WorkflowStage stage)
        {
            Console.WriteLine($"Starting {stage.Name}: Initial traffic reduction");

            // Mark stage as started
            stage.Status = WorkflowStageStatus.ReducingTraffic;
            stage.StartedAt = DateTime.UtcNow;

            // Calculate intermediate percentage (80% of original, or target if smaller reduction)
            var reductionAmount = (stage.CurrentAllocationPercentage - stage.TargetAllocationPercentage);
            var intermediatePercentage = stage.TargetAllocationPercentage;

            // If it's a large reduction, do it in phases (e.g., 100→80→0)
            if (reductionAmount >= 40)
            {
                intermediatePercentage = stage.CurrentAllocationPercentage - (int)(reductionAmount * 0.2); // Reduce by 80% of total
                intermediatePercentage = Math.Max(intermediatePercentage, stage.TargetAllocationPercentage); // But not below target
            }

            // Perform the reduction
            await ReduceTrafficAsync(stage, stage.CurrentAllocationPercentage, intermediatePercentage);
            stage.CurrentAllocationPercentage = intermediatePercentage;

            Console.WriteLine($"{stage.Name}: Traffic reduced to {intermediatePercentage}%");

            // If we've reached the target in one go, mark as completed
            if (stage.CurrentAllocationPercentage == stage.TargetAllocationPercentage)
            {
                stage.Status = WorkflowStageStatus.Completed;
                stage.CompletedAt = DateTime.UtcNow;
                await MoveToNextStageAsync();
            }
            else
            {
                // Need to wait before final reduction
                await _stateMachine.FireAsync(WorkflowTrigger.ReductionCompleted);
            }
        }

        private async Task ProcessFinalReductionAsync(WorkflowStage stage)
        {
            Console.WriteLine($"Continuing {stage.Name}: Final traffic reduction");

            stage.Status = WorkflowStageStatus.ReducingTraffic;

            // Final reduction to target percentage
            await ReduceTrafficAsync(stage, stage.CurrentAllocationPercentage, stage.TargetAllocationPercentage);
            stage.CurrentAllocationPercentage = stage.TargetAllocationPercentage;

            Console.WriteLine($"{stage.Name}: Final traffic reduction to {stage.TargetAllocationPercentage}% completed");

            // Mark stage as completed
            stage.Status = WorkflowStageStatus.Completed;
            stage.CompletedAt = DateTime.UtcNow;

            // Move to next stage
            await MoveToNextStageAsync();
        }

        private async Task ReduceTrafficAsync(WorkflowStage stage, int fromPercentage, int toPercentage)
        {
            Console.WriteLine($"Reducing traffic in {stage.Name} from {fromPercentage}% to {toPercentage}%");

            // Simulate API call to reduce traffic - in real implementation, this would call your deployment/configuration API
            await Task.Delay(5000); // Simulate API call time

            // You could implement gradual reduction here if needed:
            // var currentPerc = fromPercentage;
            // while (currentPerc > toPercentage) {
            //     currentPerc = Math.Max(currentPerc - 10, toPercentage);
            //     await CallConfigurationApi(stage.Name, currentPerc);
            //     await Task.Delay(500);
            // }
        }

        private async Task StartWaitPeriodAsync()
        {
            var currentStage = Context.ArchiveConfiguration!.CurrentStage!;

            Console.WriteLine($"Starting wait period for {currentStage.Name}: {currentStage.WaitDuration}");
            currentStage.Status = WorkflowStageStatus.Waiting;
            currentStage.WaitStartTime = DateTime.UtcNow;

            // In real implementation, you'd schedule this with a background service or job scheduler
            // For demo purposes, we'll simulate with a shorter delay
            var simulatedWaitTime = TimeSpan.FromSeconds(Math.Min(currentStage.WaitDuration.TotalSeconds, 20));
            await Task.Delay(simulatedWaitTime);

            // Complete the wait period
            await CompleteWaitPeriodAsync();
        }

        private async Task CompleteWaitPeriodAsync()
        {
            var currentStage = Context.ArchiveConfiguration!.CurrentStage!;

            Console.WriteLine($"Wait period completed for {currentStage.Name}");

            // Check if we need further reduction or can move to next stage
            if (currentStage.CurrentAllocationPercentage > currentStage.TargetAllocationPercentage)
            {
                // Need further reduction
                await _stateMachine.FireAsync(WorkflowTrigger.WaitPeriodCompleted);
            }
            else
            {
                // Stage is complete, move to next
                currentStage.Status = WorkflowStageStatus.Completed;
                currentStage.CompletedAt = DateTime.UtcNow;
                await MoveToNextStageAsync();
            }
        }

        private async Task MoveToNextStageAsync()
        {
            var config = Context.ArchiveConfiguration!;

            if (config.MoveToNextStage())
            {
                // Continue with next stage
                Console.WriteLine($"Moving to next stage: {config.CurrentStage?.Name}");
                await _stateMachine.FireAsync(WorkflowTrigger.WaitPeriodCompleted);
            }
            else
            {
                // All stages completed
                Console.WriteLine($"All stages completed for {Context.ConfigurationName}");
                await _stateMachine.FireAsync(WorkflowTrigger.Complete);
            }
        }

        public override async Task<bool> CanStartAsync()
        {
            var config = Context.ArchiveConfiguration;
            return config != null &&
                   config.Stages.Any() &&
                   config.Stages.Any(s => s.CurrentAllocationPercentage > s.TargetAllocationPercentage);
        }

        public override async Task StartAsync()
        {
            if (await CanStartAsync())
            {
                Context.ArchiveConfiguration!.WorkflowStartedAt = DateTime.UtcNow;
                await _stateMachine.FireAsync(WorkflowTrigger.Start);
            }
            else
            {
                throw new InvalidOperationException("Cannot start workflow: no stages configured or no traffic to reduce");
            }
        }

        public override async Task<string> GetCurrentStatusAsync()
        {
            var config = Context.ArchiveConfiguration;

            if (config == null)
            {
                return "No archive configuration found";
            }

            return CurrentState switch
            {
                WorkflowState.Created => "Ready to start archive process",
                WorkflowState.InProgress => config.GetStatusDescription(),
                WorkflowState.Waiting => config.GetStatusDescription(),
                WorkflowState.Completed => "✅ All stages archived successfully",
                WorkflowState.Failed => $"❌ Failed: {Context.ErrorMessage}",
                _ => "Unknown state"
            };
        }

        public List<WorkflowStage> GetStages()
        {
            return Context.ArchiveConfiguration?.Stages ?? new List<WorkflowStage>();
        }

        public double GetOverallProgress()
        {
            return Context.GetOverallProgress();
        }
    }
}