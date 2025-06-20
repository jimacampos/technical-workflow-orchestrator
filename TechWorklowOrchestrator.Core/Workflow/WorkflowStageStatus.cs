namespace TechWorklowOrchestrator.Core.Workflow
{
    public enum WorkflowStageStatus
    {
        Pending,           // Not started yet
        ReducingTraffic,   // Currently reducing traffic allocation
        Waiting,           // Waiting period after traffic reduction
        Completed,         // Stage completed successfully
        Failed             // Stage failed
    }
}
