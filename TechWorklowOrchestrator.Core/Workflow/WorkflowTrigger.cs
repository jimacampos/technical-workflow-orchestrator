using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechWorklowOrchestrator.Core.Workflow
{
    public enum WorkflowTrigger
    {
        // Common triggers
        Start,
        Complete,
        Fail,
        Timeout,
        UserProceed,

        // Archive-Only triggers
        ReductionCompleted,
        WaitPeriodCompleted,
        ArchiveCompleted,

        // Code-First triggers
        PRCreated,
        PRApproved,
        PRMerged,
        DeploymentDetected,

        // Transform-to-Default triggers
        TransformCompleted
    }
}
