using TechWorklowOrchestrator.Core;

namespace TechWorklowOrchestrator.ApiService.Dto
{
    public class CreateProjectRequest
    {
        public string Name { get; set; } = string.Empty;
        public ServiceName ServiceName { get; set; }
        public string? Description { get; set; }
    }
}
