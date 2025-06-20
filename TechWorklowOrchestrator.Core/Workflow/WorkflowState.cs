using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechWorklowOrchestrator.Core.Workflow
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
        Merging,
        WaitingForDeployment,

        // Transform-to-Default specific
        Transforming
    }
}
