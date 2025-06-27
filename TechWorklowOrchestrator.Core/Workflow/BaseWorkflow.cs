using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TechWorklowOrchestrator.Core.Workflow
{
    public abstract class BaseWorkflow<TContext, TState, TTrigger>
        where TState : Enum
        where TTrigger : Enum
    {
        protected readonly TContext Context;
        protected TState CurrentState;
        protected readonly Stateless.StateMachine<TState, TTrigger> _stateMachine;

        protected BaseWorkflow(TContext context, TState initialState)
        {
            Context = context;
            CurrentState = initialState;
            _stateMachine = new Stateless.StateMachine<TState, TTrigger>(() => CurrentState, s => CurrentState = s);
        }

        // Derived classes should configure the state machine in their constructor or a protected method
        protected abstract void ConfigureStateMachine();

        // Fire a trigger asynchronously
        public virtual async Task FireAsync(TTrigger trigger)
        {
            if (_stateMachine.CanFire(trigger))
            {
                await _stateMachine.FireAsync(trigger);
            }
        }

        // Check if a trigger can be fired
        public virtual bool CanFire(TTrigger trigger) => _stateMachine.CanFire(trigger);

        // Get the current state
        public virtual TState GetCurrentState() => CurrentState;

        // Get available triggers for the current state
        public virtual IEnumerable<TTrigger> GetPermittedTriggers() => _stateMachine.PermittedTriggers;

        // Override to provide workflow-specific start logic
        public abstract Task<bool> CanStartAsync();

        public abstract Task StartAsync();

        // Override to provide workflow-specific status
        public abstract Task<string> GetCurrentStatusAsync();

        // Optionally override to provide available actions for the current state
        public virtual List<string> GetAvailableActions() => new();
    }
}