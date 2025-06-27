namespace TechWorklowOrchestrator.Core.Workflow
{
    public enum CodeUpdateState
    {
        PRInProgress,
        ValidationInTestEnv,
        PRInReview,
        MergedAwaitingDeployment,
        DeploymentDone,
        Failed
    }
}