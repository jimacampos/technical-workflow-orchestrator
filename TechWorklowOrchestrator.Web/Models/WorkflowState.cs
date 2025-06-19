namespace TechWorklowOrchestrator.Web.Models
{
    public enum WorkflowState
    {
        Created,
        InProgress,
        Waiting,
        Completed,
        Failed,
        ReducingTo80Percent,
        WaitingAfter80Percent,
        ReducingToZero,
        Archiving,
        CreatingPR,
        AwaitingReview,
        Merging,
        WaitingForDeployment,
        Transforming
    }
}
