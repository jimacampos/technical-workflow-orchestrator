namespace TechWorklowOrchestrator.Core
{
    public class GenericProject<TContext>
        where TContext : class
    {
        public string Name { get; set; }
        public ServiceName ServiceName { get; set; }
        public List<TContext> Contexts { get; set; } = new();

        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public ProjectStatus Status { get; set; } = ProjectStatus.Active;
        public string? Description { get; set; }

        // Computed properties (require TContext to have IsCompleted property for these to work)
        public int TotalWorkflows => Contexts?.Count ?? 0;

        public int CompletedWorkflows
        {
            get
            {
                // Use reflection to check for IsCompleted property
                if (Contexts == null) return 0;
                int count = 0;
                foreach (var wf in Contexts)
                {
                    var prop = typeof(TContext).GetProperty("IsCompleted");
                    if (prop != null && prop.PropertyType == typeof(bool))
                    {
                        if ((bool)(prop.GetValue(wf) ?? false))
                            count++;
                    }
                }
                return count;
            }
        }

        public int ActiveWorkflows => TotalWorkflows - CompletedWorkflows;

        public double CompletionPercentage => TotalWorkflows == 0 ? 0 : (double)CompletedWorkflows / TotalWorkflows * 100;
    }
}