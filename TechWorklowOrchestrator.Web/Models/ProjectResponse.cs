namespace TechWorklowOrchestrator.Web.Models
{
    // Project response model
    public class ProjectResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ServiceName ServiceName { get; set; }
        public ProjectStatus Status { get; set; }
        public ProjectType Type { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int TotalWorkflows { get; set; }
        public int CompletedWorkflows { get; set; }
        public int ActiveWorkflows { get; set; }
        public double CompletionPercentage { get; set; }

        // Optional: Include actual workflows if needed
        public List<WorkflowResponse>? Workflows { get; set; }
    }
}
