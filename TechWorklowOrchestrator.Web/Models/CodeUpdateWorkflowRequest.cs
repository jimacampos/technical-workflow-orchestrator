using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TechWorklowOrchestrator.Web.Models
{
    /// <summary>
    /// Request model for creating a CodeUpdate workflow (Web project).
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
        /// Optional: Project ID associated with this code update.
        /// </summary>
        public Guid? ProjectId { get; set; }
    }

    public class CodeUpdateStageModel
    {
        public string Name { get; set; } = "";
        public bool IsComplete { get; set; } = false;
        public string? Notes { get; set; }
    }
}