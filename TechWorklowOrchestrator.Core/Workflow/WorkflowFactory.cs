using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechWorklowOrchestrator.Core.Workflow
{
    // Factory to create appropriate workflow
    public static class WorkflowFactory
    {
        public static ConfigCleanupWorkflowBase CreateWorkflow(ConfigCleanupContext context)
        {
            return context.WorkflowType switch
            {
                WorkflowType.ArchiveOnly => new ArchiveOnlyWorkflow(context),
                WorkflowType.CodeFirst => new CodeFirstWorkflow(context),
                WorkflowType.TransformToDefault => new TransformToDefaultWorkflow(context),
                _ => throw new ArgumentException($"Unknown workflow type: {context.WorkflowType}")
            };
        }
    }
}
