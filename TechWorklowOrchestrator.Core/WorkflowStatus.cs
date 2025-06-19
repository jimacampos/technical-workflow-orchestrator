namespace TechWorklowOrchestrator.Core
{
    // Workflow status enumeration
    public enum WorkflowStatus
    {
        NotStarted,
        InProgress,
        WaitingForAction,
        WaitingForReview,
        WaitingForDeployment,
        ReadyForNextStep,
        OnHold,
        Completed,
        Failed
    }
}
