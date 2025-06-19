using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Dto
{
    public class WorkflowResponse
    {
        public Guid Id { get; set; }
        public string ConfigurationName { get; set; }
        public WorkflowType WorkflowType { get; set; }
        public WorkflowState CurrentState { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUpdated { get; set; }
        public string ErrorMessage { get; set; }
        public WorkflowProgress Progress { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
    }
}
