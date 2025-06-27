namespace TechWorklowOrchestrator.Web.Models
{
    public enum WorkflowState
    {
        // Common states
        Created,
        InProgress,
        Waiting,
        Completed,
        Failed,
        AwaitingUserAction,

        // Archive-Only specific
        ReducingTo80Percent,
        WaitingAfter80Percent,
        ReducingToZero,
        Archiving,

        // Code-First specific
        CreatingPR,
        AwaitingReview,
        Merged,
        WaitingForDeployment,

        // Transform-to-Default specific
        Transforming
    }
}
