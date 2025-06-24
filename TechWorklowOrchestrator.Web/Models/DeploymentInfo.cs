namespace TechWorklowOrchestrator.Web.Models
{
    public class DeploymentInfo
    {
        public string Environment { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? DeployedAt { get; set; }
        public string DeployedBy { get; set; } = "";
        public string BuildNumber { get; set; } = "";
        public string ReleaseDefinition { get; set; } = "";
    }
}
