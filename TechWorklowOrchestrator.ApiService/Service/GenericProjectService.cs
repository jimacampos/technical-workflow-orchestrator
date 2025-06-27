using System;
using System.Collections.Generic;
using System.Linq;
using TechWorklowOrchestrator.ApiService.Repository;
using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Service
{
    public class GenericProjectService<TContext> : IGenericProjectService<TContext>
        where TContext : class
    {
        private readonly IGenericProjectRepository<TContext> _projectRepository;

        public GenericProjectService(IGenericProjectRepository<TContext> projectRepository)
        {
            _projectRepository = projectRepository;
        }

        public GenericProject<TContext> CreateProject(string name, ServiceName serviceName, string? description = null)
        {
            var project = _projectRepository.CreateProjectInstance(name, serviceName, description);
            _projectRepository.CreateProjectAsync(project).Wait();
            return project;
        }

        public List<GenericProject<TContext>> GetAllProjects()
        {
            var projects = _projectRepository.GetAllProjectsAsync().Result;
            return projects.ToList();
        }

        public List<GenericProject<TContext>> GetProjectsByService(ServiceName serviceName)
        {
            var projects = _projectRepository.GetProjectsByServiceAsync(serviceName).Result;
            return projects.ToList();
        }

        public GenericProject<TContext>? GetProjectById(Guid projectId)
        {
            return _projectRepository.GetProjectByIdAsync(projectId).Result;
        }

        public void AddWorkflowToProject(Guid projectId, TContext workflow)
        {
            var project = GetProjectById(projectId);
            if (project != null)
            {
                _projectRepository.SetWorkflowProjectId(workflow, projectId);
                _projectRepository.CreateWorkflowAsync(workflow).Wait();
                _projectRepository.AddWorkflowToProject(project, workflow);
                _projectRepository.UpdateProjectAsync(project).Wait();
            }
        }

        public TContext CreateWorkflow(Guid projectId, string configurationName, WorkflowType workflowType)
        {
            var project = GetProjectById(projectId);
            if (project == null)
                throw new ArgumentException($"Project with ID {projectId} not found");

            var workflow = _projectRepository.CreateWorkflowInstance(projectId, configurationName, workflowType);
            _projectRepository.CreateWorkflowAsync(workflow).Wait();
            return workflow;
        }

        public List<TContext> GetWorkflowsByProject(Guid projectId)
        {
            var workflows = _projectRepository.GetWorkflowsByProjectAsync(projectId).Result;
            return workflows.ToList();
        }

        public TContext? GetWorkflowById(Guid workflowId)
        {
            return _projectRepository.GetWorkflowByIdAsync(workflowId).Result;
        }

        public void UpdateWorkflow(TContext workflow)
        {
            _projectRepository.UpdateWorkflowAsync(workflow).Wait();
        }

        public List<TContext> GetAllWorkflows()
        {
            var workflows = _projectRepository.GetAllWorkflowsAsync().Result;
            return workflows.ToList();
        }
    }
}