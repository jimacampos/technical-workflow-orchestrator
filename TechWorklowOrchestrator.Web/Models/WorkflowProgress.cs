namespace TechWorklowOrchestrator.Web.Models
{
    public class WorkflowProgress
    {
        public int CurrentStep { get; set; }
        public int TotalSteps { get; set; }
        public double PercentComplete { get; set; }
        public string CurrentStepDescription { get; set; } = "";
        public DateTime? NextActionAt { get; set; }
        public bool RequiresManualAction { get; set; }
        public string ManualActionDescription { get; set; } = "";
    }
}
