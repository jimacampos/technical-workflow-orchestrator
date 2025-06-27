using System;
using System.Collections.Generic;

namespace TechWorklowOrchestrator.ApiService.Repository
{
    public class GenericWorkflowInstance
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string WorkflowDisplayName { get; set; } // e.g., PR title, update name, etc.
        public string WorkflowType { get; set; } // Use string or enum as appropriate
        public object CurrentState { get; set; }
        public object Context { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUpdated { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public List<object> History { get; set; } = new(); // Use a generic event type or interface
    }
}