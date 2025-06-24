namespace TechWorklowOrchestrator.Web.Models
{
    public class BuildSummary
    {
        public string BuildNumber { get; set; } = "";
        public string DefinitionName { get; set; } = "";
        public string Status { get; set; } = "";
        public string Result { get; set; } = "";
        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }
        public string RequestedBy { get; set; } = "";
        public TimeSpan? Duration => FinishTime - StartTime;
    }
}
