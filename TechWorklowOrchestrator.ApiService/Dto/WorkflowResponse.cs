using TechWorklowOrchestrator.Core.Workflow;
using TechWorklowOrchestrator.Web.Models;
using WorkflowState = TechWorklowOrchestrator.Core.Workflow.WorkflowState;
using WorkflowType = TechWorklowOrchestrator.Core.Workflow.WorkflowType;

namespace TechWorklowOrchestrator.ApiService.Dto
{
    public class WorkflowResponse
    {
        public Guid Id { get; set; }
        public string ConfigurationName { get; set; } = "";
        public WorkflowType WorkflowType { get; set; }
        public WorkflowState CurrentState { get; set; }
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUpdated { get; set; }
        public string? ErrorMessage { get; set; }
        public WorkflowProgress Progress { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();

        public Guid? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public ServiceName? ProjectService { get; set; }
        public string? PullRequestUrl { get; set; }
        public DateTime? CodeWorkStartedAt { get; set; }
        public DateTime? PRCreatedAt { get; set; }
        public DateTime? PRApprovedAt { get; set; }
        public DateTime? PRMergedAt { get; set; }
        public DateTime? DeploymentDetectedAt { get; set; }
    }
}
