using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.Core
{
    public class ConfigCleanupContext
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? ProjectId { get; set; } // Link to project

        public string ConfigurationName { get; set; }
        public WorkflowType WorkflowType { get; set; }
        public int CurrentTrafficPercentage { get; set; } = 100;
        public string PullRequestUrl { get; set; }
        public DateTime? WaitStartTime { get; set; }
        public TimeSpan WaitDuration { get; set; } = TimeSpan.FromHours(24);
        public string ErrorMessage { get; set; }
        public bool IsCompleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // New stage-based configuration for ArchiveOnly workflows
        public ArchiveWorkflowConfiguration? ArchiveConfiguration { get; set; }

        // Track when TransformToDefault workflows are started
        public DateTime? TransformStartedAt { get; set; }

        // Helper method to initialize archive configuration
        public void InitializeArchiveConfiguration(List<(string stageName, int currentPercentage, int targetPercentage, TimeSpan? waitDuration)> stageDefinitions)
        {
            ArchiveConfiguration = new ArchiveWorkflowConfiguration();

            foreach (var (stageName, currentPercentage, targetPercentage, waitDuration) in stageDefinitions)
            {
                ArchiveConfiguration.Stages.Add(new WorkflowStage
                {
                    Name = stageName,
                    CurrentAllocationPercentage = currentPercentage,
                    TargetAllocationPercentage = targetPercentage,
                    WaitDuration = waitDuration ?? TimeSpan.FromHours(24)
                });
            }
        }

        // Get overall workflow progress (works for both old and new style)
        public double GetOverallProgress()
        {
            if (WorkflowType == WorkflowType.ArchiveOnly && ArchiveConfiguration != null)
            {
                return ArchiveConfiguration.OverallProgress;
            }

            // For TransformToDefault workflows
            if (WorkflowType == WorkflowType.TransformToDefault)
            {
                if (IsCompleted) return 100.0;
                if (TransformStartedAt.HasValue) return 50.0; // Started but not completed
                return 0.0; // Not started
            }

            // Fallback for other workflow types or legacy archive workflows
            return IsCompleted ? 100.0 : 0.0;
        }

        // Get current workflow status description
        public string GetCurrentStatusDescription()
        {
            if (WorkflowType == WorkflowType.ArchiveOnly && ArchiveConfiguration != null)
            {
                return ArchiveConfiguration.GetStatusDescription();
            }

            // For TransformToDefault workflows
            if (WorkflowType == WorkflowType.TransformToDefault)
            {
                if (IsCompleted) return "✅ Configuration transformed to defaults";
                if (!string.IsNullOrEmpty(ErrorMessage)) return $"❌ Failed: {ErrorMessage}";
                if (TransformStartedAt.HasValue) return "⏳ Ready to transform to default values";
                return "Ready to start transformation";
            }

            // Fallback for other workflow types
            return IsCompleted ? "Completed" : "In Progress";
        }
    }
}