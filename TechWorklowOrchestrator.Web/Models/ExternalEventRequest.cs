using System.ComponentModel.DataAnnotations;

namespace TechWorklowOrchestrator.Web.Models
{
    public class ExternalEventRequest
    {
        [Required]
        public string EventType { get; set; } = "";
        public Dictionary<string, object> Data { get; set; } = new();
    }
}
