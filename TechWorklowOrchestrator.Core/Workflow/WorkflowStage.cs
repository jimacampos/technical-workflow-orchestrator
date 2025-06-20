using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechWorklowOrchestrator.Core.Workflow
{
    // Represents a deployment stage (dev, test, prod, etc.)
    public class WorkflowStage
    {
        public string Name { get; set; } = string.Empty;
        public int CurrentAllocationPercentage { get; set; }
        public int TargetAllocationPercentage { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public WorkflowStageStatus Status { get; set; } = WorkflowStageStatus.Pending;
        public string? ErrorMessage { get; set; }
        public TimeSpan WaitDuration { get; set; } = TimeSpan.FromHours(24); // Default wait time
        public DateTime? WaitStartTime { get; set; }
    }
}
