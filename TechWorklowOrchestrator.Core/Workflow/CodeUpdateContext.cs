using System;

namespace TechWorklowOrchestrator.Core.Workflow
{
    /// <summary>
    /// Context for the CodeUpdateWorkflow, tracks all relevant state and metadata.
    /// </summary>
    public class CodeUpdateContext
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? ProjectId { get; set; }

        /// <summary>
        /// Name or identifier for this code update (e.g., branch, PR, or feature name).
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// URL to the pull request associated with this update.
        /// </summary>
        public string? PullRequestUrl { get; set; }

        /// <summary>
        /// Error message if the workflow fails.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Indicates if the workflow is completed.
        /// </summary>
        public bool IsCompleted { get; set; } = false;

        /// <summary>
        /// When the workflow was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the workflow was started.
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// When the workflow was completed.
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        // Stage-specific timestamps
        public DateTime? PRInProgressAt { get; set; }
        public DateTime? ValidationInTestEnvAt { get; set; }
        public DateTime? PRInReviewAt { get; set; }
        public DateTime? MergedAwaitingDeploymentAt { get; set; }
        public DateTime? DeploymentDoneAt { get; set; }

        /// <summary>
        /// Optionally, track progress (0-100).
        /// </summary>
        public double Progress { get; set; } = 0.0;
        public string Description { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
        public WorkflowType WorkflowType { get; set; }

        /// <summary>
        /// Returns a human-readable status for the workflow.
        /// </summary>
        public string GetStatusDescription()
        {
            if (IsCompleted) return "✅ Code update deployed";
            if (!string.IsNullOrEmpty(ErrorMessage)) return $"❌ Failed: {ErrorMessage}";
            return "Not started";
        }

        public string GetCurrentStatusDescription()
        {
            if (IsCompleted)
                return "✅ Code update deployed";
            if (!string.IsNullOrEmpty(ErrorMessage))
                return $"❌ Failed: {ErrorMessage}";

            if (DeploymentDoneAt.HasValue)
                return "✅ Code update deployed";
            if (MergedAwaitingDeploymentAt.HasValue)
                return "⏳ Waiting for deployment";
            if (PRInReviewAt.HasValue)
                return "🔄 In PR review";
            if (ValidationInTestEnvAt.HasValue)
                return "🔬 Validating in test environment";
            if (PRInProgressAt.HasValue)
                return "📝 PR in progress";
            if (StartedAt.HasValue)
                return "🔄 Workflow started";

            return "Ready to start code update";
        }
    }
}