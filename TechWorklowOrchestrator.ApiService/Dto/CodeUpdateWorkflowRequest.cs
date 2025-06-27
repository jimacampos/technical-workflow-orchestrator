using System.ComponentModel.DataAnnotations;

namespace TechWorklowOrchestrator.ApiService.Dto
{
    /// <summary>
    /// Request model for creating a CodeUpdate workflow.
    /// </summary>
    public class CodeUpdateWorkflowRequest
    {
        /// <summary>
        /// Name or identifier for this code update (e.g., branch, PR, or feature name).
        /// </summary>
        [Required]
        [StringLength(200, MinimumLength = 3)]
        public string Title { get; set; } = "";

        [StringLength(500)]
        public string Description { get; set; } = "";

        public List<CodeUpdateStageModel> Stages { get; set; } = new();

        /// <summary>
        /// Optional: Metadata for the workflow.
        /// </summary>
        public Dictionary<string, string>? Metadata { get; set; } = new();

        /// <summary>
        /// Optional: GenericProject ID associated with this code update.
        /// </summary>
        public Guid? ProjectId { get; set; }
    }
}