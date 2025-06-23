using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Service
{
    public class ProjectService : IProjectService
    {
        private readonly List<Project> _projects = new List<Project>();

        public Project CreateProject(string name, ServiceName serviceName, string? description = null)
        {
            var project = new Project
            {
                Name = name,
                ServiceName = serviceName,
                Description = description,
                Status = ProjectStatus.Active
            };

            _projects.Add(project);
            return project;
        }

        public List<Project> GetAllProjects()
        {
            return _projects.ToList();
        }

        public List<Project> GetProjectsByService(ServiceName serviceName)
        {
            return _projects.Where(p => p.ServiceName == serviceName).ToList();
        }

        public Project? GetProjectById(Guid projectId)
        {
            return _projects.FirstOrDefault(p => p.Id == projectId);
        }

        public void AddWorkflowToProject(Guid projectId, ConfigCleanupContext workflow)
        {
            var project = GetProjectById(projectId);
            if (project != null)
            {
                project.CleanupContexts.Add(workflow);
            }
        }

        public ConfigCleanupContext CreateWorkflow(Guid projectId, string configurationName, WorkflowType workflowType)
        {
            var project = GetProjectById(projectId);
            if (project == null)
            {
                throw new ArgumentException($"Project with ID {projectId} not found");
            }

            var workflow = new ConfigCleanupContext
            {
                ProjectId = projectId,
                ConfigurationName = configurationName,
                WorkflowType = workflowType,
                CreatedAt = DateTime.UtcNow
            };

            project.CleanupContexts.Add(workflow);
            return workflow;
        }

        public List<ConfigCleanupContext> GetWorkflowsByProject(Guid projectId)
        {
            var project = GetProjectById(projectId);
            return project?.CleanupContexts ?? new List<ConfigCleanupContext>();
        }

        public ConfigCleanupContext? GetWorkflowById(Guid workflowId)
        {
            return _projects
                .SelectMany(p => p.CleanupContexts)
                .FirstOrDefault(w => w.Id == workflowId);
        }

        public void UpdateWorkflow(ConfigCleanupContext workflow)
        {
            var existingWorkflow = GetWorkflowById(workflow.Id);
            if (existingWorkflow != null)
            {
                // Update properties
                existingWorkflow.CurrentTrafficPercentage = workflow.CurrentTrafficPercentage;
                existingWorkflow.PullRequestUrl = workflow.PullRequestUrl;
                existingWorkflow.WaitStartTime = workflow.WaitStartTime;
                existingWorkflow.WaitDuration = workflow.WaitDuration;
                existingWorkflow.ErrorMessage = workflow.ErrorMessage;
                existingWorkflow.IsCompleted = workflow.IsCompleted;

                // Update TransformToDefault specific properties
                existingWorkflow.TransformStartedAt = workflow.TransformStartedAt;

                // Update ArchiveOnly specific properties
                existingWorkflow.ArchiveConfiguration = workflow.ArchiveConfiguration;
            }
        }

        public List<ConfigCleanupContext> GetAllWorkflows()
        {
            return _projects.SelectMany(p => p.CleanupContexts).ToList();
        }
    }
}
