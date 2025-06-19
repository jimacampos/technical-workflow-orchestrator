using System.ComponentModel.DataAnnotations;

namespace TechWorklowOrchestrator.Web.Models
{
    public class CreateWorkflowRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string ConfigurationName { get; set; } = "";

        [Required]
        public WorkflowType WorkflowType { get; set; }

        [Range(0, 100)]
        public int CurrentTrafficPercentage { get; set; } = 100;

        public TimeSpan? CustomWaitDuration { get; set; }

        [StringLength(500)]
        public string Description { get; set; } = "";

        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
