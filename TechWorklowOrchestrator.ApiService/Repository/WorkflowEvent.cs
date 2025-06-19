using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Repository
{
    public class WorkflowEvent
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string EventType { get; set; }
        public WorkflowState FromState { get; set; }
        public WorkflowState ToState { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }
}
