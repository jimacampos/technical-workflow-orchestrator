using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Dto
{
    public class WorkflowSummary
    {
        public int TotalWorkflows { get; set; }
        public int ActiveWorkflows { get; set; }
        public int CompletedWorkflows { get; set; }
        public int FailedWorkflows { get; set; }
        public int AwaitingManualAction { get; set; }
        public Dictionary<WorkflowType, int> ByType { get; set; } = new();
        public Dictionary<WorkflowState, int> ByState { get; set; } = new();
    }
}
