using System.ComponentModel.DataAnnotations;

namespace TechWorklowOrchestrator.ApiService.Dto
{
    public class ExternalEventRequest
    {
        [Required]
        public string EventType { get; set; }

        public Dictionary<string, object> Data { get; set; } = new();
    }
}
