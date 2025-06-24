namespace TechWorklowOrchestrator.Web.Models
{
    public class PullRequestSummary
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Status { get; set; } = "";
        public string SourceBranch { get; set; } = "";
        public string TargetBranch { get; set; } = "";
        public string Author { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public string Repository { get; set; } = "";
    }
}
