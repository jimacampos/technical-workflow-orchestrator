namespace TechWorklowOrchestrator.ApiService.Dto
{
    public class ArchiveStageModel
    {
        public string Name { get; set; } = "";
        public int CurrentPercentage { get; set; } = 100;
        public int TargetPercentage { get; set; } = 0;
        public double WaitHours { get; set; } = 24;
    }
}
