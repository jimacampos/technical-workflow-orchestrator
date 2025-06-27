using System.Collections.Concurrent;
using System.Reflection;
using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Repository
{
    public class InMemoryGenericProjectRepository<TContext> : IGenericProjectRepository<TContext>
        where TContext : class
    {
        private readonly ConcurrentDictionary<Guid, GenericProject<TContext>> _projects = new();
        private readonly ConcurrentDictionary<Guid, TContext> _workflows = new();

        public Task<Guid> CreateProjectAsync(GenericProject<TContext> project)
        {
            var id = project.Id;
            if (id == Guid.Empty)
            {
                id = Guid.NewGuid();
                project.Id = id;
            }
            _projects[id] = project;
            return Task.FromResult(id);
        }

        public Task<GenericProject<TContext>> GetProjectByIdAsync(Guid id)
        {
            _projects.TryGetValue(id, out var project);
            return Task.FromResult(project);
        }

        public Task<IEnumerable<GenericProject<TContext>>> GetAllProjectsAsync()
        {
            return Task.FromResult(_projects.Values.AsEnumerable());
        }

        public Task<IEnumerable<GenericProject<TContext>>> GetProjectsByServiceAsync(ServiceName serviceName)
        {
            var projects = _projects.Values
                .Where(p => p.ServiceName == serviceName);
            return Task.FromResult(projects);
        }

        public Task UpdateProjectAsync(GenericProject<TContext> project)
        {
            var id = project.Id;
            if (_projects.ContainsKey(id))
            {
                _projects[id] = project;
            }
            return Task.CompletedTask;
        }

        public Task DeleteProjectAsync(Guid id)
        {
            _projects.TryRemove(id, out _);

            // Remove associated workflows
            var workflowsToRemove = _workflows.Values
                .Where(w => GetProjectIdFromWorkflow(w) == id)
                .Select(w => GetWorkflowId(w))
                .ToList();

            foreach (var workflowId in workflowsToRemove)
            {
                _workflows.TryRemove(workflowId, out _);
            }

            return Task.CompletedTask;
        }

        public Task<Guid> CreateWorkflowAsync(TContext workflow)
        {
            var id = GetWorkflowId(workflow);
            if (id == Guid.Empty)
            {
                id = Guid.NewGuid();
                SetWorkflowId(workflow, id);
            }

            // Set CreatedAt if exists
            var createdAtProp = typeof(TContext).GetProperty("CreatedAt");
            if (createdAtProp != null && createdAtProp.PropertyType == typeof(DateTime))
            {
                createdAtProp.SetValue(workflow, DateTime.UtcNow);
            }

            _workflows[id] = workflow;
            return Task.FromResult(id);
        }

        public Task<TContext> GetWorkflowByIdAsync(Guid id)
        {
            _workflows.TryGetValue(id, out var workflow);
            return Task.FromResult(workflow);
        }

        public Task<IEnumerable<TContext>> GetAllWorkflowsAsync()
        {
            return Task.FromResult(_workflows.Values.AsEnumerable());
        }

        public Task<IEnumerable<TContext>> GetWorkflowsByProjectAsync(Guid projectId)
        {
            var workflows = _workflows.Values
                .Where(w => GetProjectIdFromWorkflow(w) == projectId);
            return Task.FromResult(workflows);
        }

        public Task<IEnumerable<TContext>> GetWorkflowsByTypeAsync(WorkflowType workflowType)
        {
            var workflows = _workflows.Values
                .Where(w => GetWorkflowType(w) == workflowType);
            return Task.FromResult(workflows);
        }

        public Task UpdateWorkflowAsync(TContext workflow)
        {
            var id = GetWorkflowId(workflow);
            if (_workflows.ContainsKey(id))
            {
                _workflows[id] = workflow;
            }
            return Task.CompletedTask;
        }

        public Task DeleteWorkflowAsync(Guid id)
        {
            _workflows.TryRemove(id, out _);
            return Task.CompletedTask;
        }

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
            var count = _workflows.Values.Count(w => GetProjectIdFromWorkflow(w) == projectId);
            return Task.FromResult(count);
        }

        // Factory/helper methods for generic service support
        public GenericProject<TContext> CreateProjectInstance(string name, ServiceName serviceName, string? description = null)
        {
            var project = new GenericProject<TContext>
            {
                Name = name,
                ServiceName = serviceName,
                Description = description,
                Status = ProjectStatus.Active,
                CreatedAt = DateTime.UtcNow,
                Id = Guid.NewGuid()
            };
            return project;
        }

        public TContext CreateWorkflowInstance(Guid projectId, string configurationName, WorkflowType workflowType)
        {
            var workflow = Activator.CreateInstance<TContext>();
            typeof(TContext).GetProperty("ProjectId")?.SetValue(workflow, projectId);
            typeof(TContext).GetProperty("ConfigurationName")?.SetValue(workflow, configurationName);
            typeof(TContext).GetProperty("WorkflowType")?.SetValue(workflow, workflowType);
            typeof(TContext).GetProperty("CreatedAt")?.SetValue(workflow, DateTime.UtcNow);
            typeof(TContext).GetProperty("Id")?.SetValue(workflow, Guid.NewGuid());
            // Set Title if present
            typeof(TContext).GetProperty("Title")?.SetValue(workflow, configurationName);
            return workflow;
        }

        public void SetWorkflowProjectId(TContext workflow, Guid projectId)
        {
            typeof(TContext).GetProperty("ProjectId")?.SetValue(workflow, projectId);
        }

        public void AddWorkflowToProject(GenericProject<TContext> project, TContext workflow)
        {
            project.Contexts.Add(workflow);
        }

        // Reflection helpers
        private static Guid GetWorkflowId(TContext workflow)
        {
            var idProp = typeof(TContext).GetProperty("Id");
            return idProp != null ? (Guid)(idProp.GetValue(workflow) ?? Guid.Empty) : Guid.Empty;
        }

        private static void SetWorkflowId(TContext workflow, Guid id)
        {
            typeof(TContext).GetProperty("Id")?.SetValue(workflow, id);
        }

        private static Guid GetProjectIdFromWorkflow(TContext workflow)
        {
            var prop = typeof(TContext).GetProperty("ProjectId");
            return prop != null ? (Guid)(prop.GetValue(workflow) ?? Guid.Empty) : Guid.Empty;
        }

        private static WorkflowType GetWorkflowType(TContext workflow)
        {
            var prop = typeof(TContext).GetProperty("WorkflowType");
            return prop != null ? (WorkflowType)(prop.GetValue(workflow) ?? WorkflowType.ArchiveOnly) : WorkflowType.ArchiveOnly;
        }
    }
}