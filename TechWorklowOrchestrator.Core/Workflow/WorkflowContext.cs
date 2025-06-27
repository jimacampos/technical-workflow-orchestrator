using System;

namespace TechWorklowOrchestrator.Core.Workflow
{
    /// <summary>
    /// Generic context for workflow operations.
    /// Extend this class as needed for specific workflow data.
    /// </summary>
    public class WorkflowContext
    {
        /// <summary>
        /// Optional: Name or identifier for the workflow instance.
        /// </summary>
        public string? WorkflowName { get; set; }

        /// <summary>
        /// Optional: Stores the time the workflow was started.
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// Optional: Stores the time the workflow was completed.
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Optional: Stores the last error message, if any.
        /// </summary>
        public string? ErrorMessage { get; set; }

        // Add more generic properties as needed for your workflows.
    }
}