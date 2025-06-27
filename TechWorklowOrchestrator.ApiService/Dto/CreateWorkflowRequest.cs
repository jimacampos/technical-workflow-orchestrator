using System.ComponentModel.DataAnnotations;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Dto
{
    public class CreateWorkflowRequest : IValidatableObject
    {
        public string? ConfigurationName { get; set; }

        [Required]
        public WorkflowType WorkflowType { get; set; }

        public int CurrentTrafficPercentage { get; set; } = 100;

        public TimeSpan? CustomWaitDuration { get; set; }

        public string Description { get; set; }

        public Dictionary<string, string>? Metadata { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            Console.WriteLine($"[DEBUG] WorkflowType: {WorkflowType}");
            Console.WriteLine($"[DEBUG] ConfigurationName: '{ConfigurationName}'");

            if (WorkflowType != WorkflowType.CodeUpdate)
            {
                if (string.IsNullOrWhiteSpace(ConfigurationName))
                {
                    yield return new ValidationResult(
                        "The ConfigurationName field is required.",
                        new[] { nameof(ConfigurationName) });
                }
                else if (ConfigurationName.Length < 3 || ConfigurationName.Length > 100)
                {
                    yield return new ValidationResult(
                        "ConfigurationName must be between 3 and 100 characters.",
                        new[] { nameof(ConfigurationName) });
                }
            }
        }
    }
}
