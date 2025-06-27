namespace TechWorklowOrchestrator.Core.Workflow
{
    public enum CodeUpdateTrigger
    {
        StartPR,
        ValidateInTest,
        SubmitForReview,
        ApproveAndMerge,
        DetectDeployment,
        Fail
    }
}