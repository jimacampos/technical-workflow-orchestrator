using TechWorklowOrchestrator.ApiService.Repository;
using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Service
{
    public class ProjectService : IProjectService
    {
        private readonly IProjectRepository _projectRepository;

        public ProjectService(IProjectRepository projectRepository)
        {
            _projectRepository = projectRepository;
        }

        public Project CreateProject(string name, ServiceName serviceName, string? description = null)
        {
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = name,
                ServiceName = serviceName,
                Description = description,
                Status = ProjectStatus.Active,
                CleanupContexts = new List<ConfigCleanupContext>()
            };

            _projectRepository.CreateProjectAsync(project).Wait();
            return project;
        }

        public List<Project> GetAllProjects()
        {
            var projects = _projectRepository.GetAllProjectsAsync().Result;
            return projects.ToList();
        }

        public List<Project> GetProjectsByService(ServiceName serviceName)
        {
            var projects = _projectRepository.GetProjectsByServiceAsync(serviceName).Result;
            return projects.ToList();
        }

        public Project? GetProjectById(Guid projectId)
        {
            return _projectRepository.GetProjectByIdAsync(projectId).Result;
        }

        public void AddWorkflowToProject(Guid projectId, ConfigCleanupContext workflow)
        {
            var project = GetProjectById(projectId);
            if (project != null)
            {
                workflow.ProjectId = projectId;
                _projectRepository.CreateWorkflowAsync(workflow).Wait();

                // Also add to the project's collection for consistency
                project.CleanupContexts.Add(workflow);
                _projectRepository.UpdateProjectAsync(project).Wait();
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
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ConfigurationName = configurationName,
                WorkflowType = workflowType,
                CreatedAt = DateTime.UtcNow
            };

            _projectRepository.CreateWorkflowAsync(workflow).Wait();
            return workflow;
        }

        public List<ConfigCleanupContext> GetWorkflowsByProject(Guid projectId)
        {
            var workflows = _projectRepository.GetWorkflowsByProjectAsync(projectId).Result;
            return workflows.ToList();
        }

        public ConfigCleanupContext? GetWorkflowById(Guid workflowId)
        {
            return _projectRepository.GetWorkflowByIdAsync(workflowId).Result;
        }

        public void UpdateWorkflow(ConfigCleanupContext workflow)
        {
            _projectRepository.UpdateWorkflowAsync(workflow).Wait();
        }

        public List<ConfigCleanupContext> GetAllWorkflows()
        {
            var workflows = _projectRepository.GetAllWorkflowsAsync().Result;
            return workflows.ToList();
        }
    }
}