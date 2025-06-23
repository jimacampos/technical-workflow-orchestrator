using System.Collections.Concurrent;
using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Repository
{
    public class InMemoryProjectRepository : IProjectRepository
    {
        private readonly ConcurrentDictionary<Guid, Project> _projects = new();
        private readonly ConcurrentDictionary<Guid, ConfigCleanupContext> _workflows = new();

        public Task<Guid> CreateProjectAsync(Project project)
        {
            if (project.Id == Guid.Empty)
            {
                project.Id = Guid.NewGuid();
            }

            _projects[project.Id] = project;
            return Task.FromResult(project.Id);
        }

        public Task<Project> GetProjectByIdAsync(Guid id)
        {
            _projects.TryGetValue(id, out var project);
            return Task.FromResult(project);
        }

        public Task<IEnumerable<Project>> GetAllProjectsAsync()
        {
            return Task.FromResult(_projects.Values.AsEnumerable());
        }

        public Task<IEnumerable<Project>> GetProjectsByServiceAsync(ServiceName serviceName)
        {
            var projects = _projects.Values.Where(p => p.ServiceName == serviceName);
            return Task.FromResult(projects);
        }

        public Task UpdateProjectAsync(Project project)
        {
            if (_projects.ContainsKey(project.Id))
            {
                _projects[project.Id] = project;
            }
            return Task.CompletedTask;
        }

        public Task DeleteProjectAsync(Guid id)
        {
            _projects.TryRemove(id, out _);

            // Also remove associated workflows
            var workflowsToRemove = _workflows.Values
                .Where(w => w.ProjectId == id)
                .Select(w => w.Id)
                .ToList();

            foreach (var workflowId in workflowsToRemove)
            {
                _workflows.TryRemove(workflowId, out _);
            }

            return Task.CompletedTask;
        }

        // Workflow-related methods
        public Task<Guid> CreateWorkflowAsync(ConfigCleanupContext workflow)
        {
            if (workflow.Id == Guid.Empty)
            {
                workflow.Id = Guid.NewGuid();
            }

            workflow.CreatedAt = DateTime.UtcNow;
            _workflows[workflow.Id] = workflow;
            return Task.FromResult(workflow.Id);
        }

        public Task<ConfigCleanupContext> GetWorkflowByIdAsync(Guid id)
        {
            _workflows.TryGetValue(id, out var workflow);
            return Task.FromResult(workflow);
        }

        public Task<IEnumerable<ConfigCleanupContext>> GetAllWorkflowsAsync()
        {
            return Task.FromResult(_workflows.Values.AsEnumerable());
        }

        public Task<IEnumerable<ConfigCleanupContext>> GetWorkflowsByProjectAsync(Guid projectId)
        {
            var workflows = _workflows.Values.Where(w => w.ProjectId == projectId);
            return Task.FromResult(workflows);
        }

        public Task<IEnumerable<ConfigCleanupContext>> GetWorkflowsByTypeAsync(WorkflowType workflowType)
        {
            var workflows = _workflows.Values.Where(w => w.WorkflowType == workflowType);
            return Task.FromResult(workflows);
        }

        public Task UpdateWorkflowAsync(ConfigCleanupContext workflow)
        {
            if (_workflows.ContainsKey(workflow.Id))
            {
                _workflows[workflow.Id] = workflow;
            }
            return Task.CompletedTask;
        }

        public Task DeleteWorkflowAsync(Guid id)
        {
            _workflows.TryRemove(id, out _);
            return Task.CompletedTask;
        }

        // Additional utility methods
        public Task<bool> ProjectExistsAsync(Guid projectId)
        {
            return Task.FromResult(_projects.ContainsKey(projectId));
        }

        public Task<int> GetProjectCountAsync()
        {
            return Task.FromResult(_projects.Count);
        }

        public Task<int> GetWorkflowCountAsync()
        {
            return Task.FromResult(_workflows.Count);
        }

        public Task<int> GetWorkflowCountByProjectAsync(Guid projectId)
        {
            var count = _workflows.Values.Count(w => w.ProjectId == projectId);
            return Task.FromResult(count);
        }
    }
}