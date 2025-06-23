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

        // CodeFirst-specific properties
        public DateTime? CodeWorkStartedAt { get; set; }
        public DateTime? PRCreatedAt { get; set; }
        public DateTime? PRApprovedAt { get; set; }
        public DateTime? PRMergedAt { get; set; }
        public DateTime? DeploymentDetectedAt { get; set; }

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

            // For CodeFirst workflows
            if (WorkflowType == WorkflowType.CodeFirst)
            {
                if (IsCompleted) return 100.0;

                var completedSteps = 0;
                if (CodeWorkStartedAt.HasValue) completedSteps++;
                if (PRCreatedAt.HasValue) completedSteps++;
                if (PRApprovedAt.HasValue) completedSteps++;
                if (PRMergedAt.HasValue) completedSteps++;
                if (DeploymentDetectedAt.HasValue) completedSteps++;

                // 5 total steps: code work, PR creation, approval, merge, deployment
                return (completedSteps / 5.0) * 100.0;
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

            // For CodeFirst workflows
            if (WorkflowType == WorkflowType.CodeFirst)
            {
                if (IsCompleted) return "✅ Code removed and deployed";
                if (!string.IsNullOrEmpty(ErrorMessage)) return $"❌ Failed: {ErrorMessage}";

                if (DeploymentDetectedAt.HasValue) return "✅ Code removed and deployed";
                if (PRMergedAt.HasValue) return "⏳ Waiting for deployment";
                if (PRApprovedAt.HasValue) return "🔄 Ready to confirm merge";
                if (PRCreatedAt.HasValue) return $"⏳ Awaiting PR review: {PullRequestUrl}";
                if (CodeWorkStartedAt.HasValue) return "🔄 Ready to create pull request";

                return "Ready to start code changes";
            }

            // Fallback for other workflow types
            return IsCompleted ? "Completed" : "In Progress";
        }
    }
}