using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Repository
{
    public class WorkflowInstance
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ConfigurationName { get; set; }
        public WorkflowType WorkflowType { get; set; }
        public WorkflowState CurrentState { get; set; }
        public ConfigCleanupContext Context { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUpdated { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public List<WorkflowEvent> History { get; set; } = new();
    }
}
