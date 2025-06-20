namespace TechWorklowOrchestrator.Core.Workflow
{
    // Configuration for the entire archive workflow
    public class ArchiveWorkflowConfiguration
    {
        public List<WorkflowStage> Stages { get; set; } = new List<WorkflowStage>();
        public int CurrentStageIndex { get; set; } = 0;
        public DateTime? WorkflowStartedAt { get; set; }
        public DateTime? WorkflowCompletedAt { get; set; }

        // Get the currently active stage
        public WorkflowStage? CurrentStage =>
            CurrentStageIndex < Stages.Count ? Stages[CurrentStageIndex] : null;

        // Check if all stages are completed
        public bool AllStagesCompleted =>
            Stages.All(s => s.Status == WorkflowStageStatus.Completed);

        // Get overall progress percentage
        public double OverallProgress
        {
            get
            {
                if (Stages.Count == 0) return 0;

                var completedStages = Stages.Count(s => s.Status == WorkflowStageStatus.Completed);
                var currentStageProgress = 0.0;

                // Add partial progress for current stage
                if (CurrentStage != null && CurrentStage.Status == WorkflowStageStatus.ReducingTraffic)
                {
                    currentStageProgress = 0.5; // 50% when reducing traffic
                }
                else if (CurrentStage != null && CurrentStage.Status == WorkflowStageStatus.Waiting)
                {
                    currentStageProgress = 0.75; // 75% when waiting
                }

                return ((completedStages + currentStageProgress) / Stages.Count) * 100;
            }
        }

        // Get current status description
        public string GetStatusDescription()
        {
            if (CurrentStage == null)
            {
                return AllStagesCompleted ? "All stages completed" : "No stages configured";
            }

            return CurrentStage.Status switch
            {
                WorkflowStageStatus.Pending => $"Ready to start stage: {CurrentStage.Name}",
                WorkflowStageStatus.ReducingTraffic => $"Reducing traffic in {CurrentStage.Name} from {CurrentStage.CurrentAllocationPercentage}% to {CurrentStage.TargetAllocationPercentage}%",
                WorkflowStageStatus.Waiting => $"Waiting in {CurrentStage.Name} (started: {CurrentStage.WaitStartTime:yyyy-MM-dd HH:mm})",
                WorkflowStageStatus.Completed => $"Stage {CurrentStage.Name} completed",
                WorkflowStageStatus.Failed => $"Stage {CurrentStage.Name} failed: {CurrentStage.ErrorMessage}",
                _ => "Unknown status"
            };
        }

        // Move to next stage
        public bool MoveToNextStage()
        {
            if (CurrentStageIndex < Stages.Count - 1)
            {
                CurrentStageIndex++;
                return true;
            }
            return false;
        }
    }
}
