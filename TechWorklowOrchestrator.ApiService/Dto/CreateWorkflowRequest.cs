using System.ComponentModel.DataAnnotations;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Dto
{
    public class CreateWorkflowRequest
    {
        [Required]
        public string ConfigurationName { get; set; }

        [Required]
        public WorkflowType WorkflowType { get; set; }

        public int CurrentTrafficPercentage { get; set; } = 100;

        public TimeSpan? CustomWaitDuration { get; set; }

        public string Description { get; set; }

        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
