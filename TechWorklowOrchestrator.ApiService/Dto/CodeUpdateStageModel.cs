namespace TechWorklowOrchestrator.ApiService.Dto
{
    public class CodeUpdateStageModel
    {
        public string Name { get; set; } = "";
        public bool IsComplete { get; set; } = false;
        public string? Notes { get; set; }
    }
}