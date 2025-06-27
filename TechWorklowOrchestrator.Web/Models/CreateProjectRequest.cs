namespace TechWorklowOrchestrator.Web.Models
{
    // Request model for creating projects
    public class CreateProjectRequest
    {
        public string Name { get; set; } = string.Empty;
        public ServiceName ServiceName { get; set; }
        public string? Description { get; set; }
        public ProjectType ProjectType { get; set; } // <-- Add this property
    }

    public enum ProjectType
    {
        ConfigCleanup,
        CodeUpdate
    }
}
