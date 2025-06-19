using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.Core
{
    public class ConfigCleanupContext
    {
        public string ConfigurationName { get; set; }
        public WorkflowType WorkflowType { get; set; }
        public int CurrentTrafficPercentage { get; set; } = 100;
        public string PullRequestUrl { get; set; }
        public DateTime? WaitStartTime { get; set; }
        public TimeSpan WaitDuration { get; set; } = TimeSpan.FromHours(24);
        public string ErrorMessage { get; set; }
    }
}
