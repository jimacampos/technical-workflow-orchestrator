using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechWorklowOrchestrator.Core.Workflow
{
    // Base workflow class
    public abstract class ConfigCleanupWorkflowBase
    {
        protected readonly StateMachine<WorkflowState, WorkflowTrigger> _stateMachine;
        public readonly ConfigCleanupContext Context;

        protected ConfigCleanupWorkflowBase(ConfigCleanupContext context, WorkflowState initialState)
        {
            Context = context;
            _stateMachine = new StateMachine<WorkflowState, WorkflowTrigger>(initialState);

            // Common error handling
            _stateMachine.OnUnhandledTrigger((state, trigger) =>
            {
                Console.WriteLine($"Unhandled trigger {trigger} in state {state}");
            });
        }

        public WorkflowState CurrentState => _stateMachine.State;
        public abstract Task<bool> CanStartAsync();
        public abstract Task StartAsync();
        public abstract Task<string> GetCurrentStatusAsync();
    }
}
