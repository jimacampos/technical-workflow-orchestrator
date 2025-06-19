namespace TechWorklowOrchestrator.Core
{
    // New Project class
    public class Project
    {
        public string Name { get; set; }
        public ServiceName ServiceName { get; set; }
        public List<ConfigCleanupContext> CleanupContexts { get; set; }
    }
}
