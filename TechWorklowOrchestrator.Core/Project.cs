namespace TechWorklowOrchestrator.Core
{
    // New Project class
    public class Project
    {
        public string Name { get; set; }
        public ServiceName ServiceName { get; set; }
        public List<ConfigCleanupContext> CleanupContexts { get; set; } = new();

        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public ProjectStatus Status { get; set; } = ProjectStatus.Active;
        public string? Description { get; set; }

        // Simple computed properties (no complex logic yet)
        public int TotalWorkflows => CleanupContexts?.Count ?? 0;
        public int CompletedWorkflows => CleanupContexts?.Count(c => c.IsCompleted) ?? 0;
        public int ActiveWorkflows => TotalWorkflows - CompletedWorkflows;
        public double CompletionPercentage => TotalWorkflows == 0 ? 0 : (double)CompletedWorkflows / TotalWorkflows * 100;
    }
}
