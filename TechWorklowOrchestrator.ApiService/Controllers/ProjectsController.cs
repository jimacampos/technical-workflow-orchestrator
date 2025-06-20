using Microsoft.AspNetCore.Mvc;
using TechWorklowOrchestrator.ApiService.Dto;
using TechWorklowOrchestrator.ApiService.Service;
using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly IProjectService _projectService;

        public ProjectsController(IProjectService projectService)
        {
            _projectService = projectService;
        }

        [HttpGet]
        public ActionResult<List<Project>> GetAllProjects()
        {
            var projects = _projectService.GetAllProjects();
            return Ok(projects);
        }

        [HttpGet("{id}")]
        public ActionResult<Project> GetProject(Guid id)
        {
            var project = _projectService.GetProjectById(id);
            if (project == null)
                return NotFound();
            return Ok(project);
        }

        [HttpGet("service/{serviceName}")]
        public ActionResult<List<Project>> GetProjectsByService(ServiceName serviceName)
        {
            var projects = _projectService.GetProjectsByService(serviceName);
            return Ok(projects);
        }

        [HttpPost]
        public ActionResult<Project> CreateProject([FromBody] CreateProjectRequest request)
        {
            var project = _projectService.CreateProject(request.Name, request.ServiceName, request.Description);
            return CreatedAtAction(nameof(GetProject), new { id = project.Id }, project);
        }

        [HttpGet("{projectId}/workflows")]
        public ActionResult<List<ConfigCleanupContext>> GetWorkflowsByProject(Guid projectId)
        {
            var project = _projectService.GetProjectById(projectId);
            if (project == null)
                return NotFound($"Project with ID {projectId} not found");

            var workflows = _projectService.GetWorkflowsByProject(projectId);
            return Ok(workflows);
        }

        [HttpPost("{projectId}/workflows")]
        public ActionResult<ConfigCleanupContext> CreateWorkflow(Guid projectId, [FromBody] CreateWorkflowRequest request)
        {
            try
            {
                // Create the basic workflow context
                var workflow = _projectService.CreateWorkflow(projectId, request.ConfigurationName, request.WorkflowType);

                // If it's an ArchiveOnly workflow and has stage configuration, parse and initialize it
                if (request.WorkflowType == WorkflowType.ArchiveOnly &&
                    request.Metadata != null &&
                    request.Metadata.ContainsKey("archiveStages"))
                {
                    try
                    {
                        var stageConfigJson = request.Metadata["archiveStages"];
                        var stageModels = System.Text.Json.JsonSerializer.Deserialize<List<ArchiveStageModel>>(stageConfigJson);

                        if (stageModels?.Any() == true)
                        {
                            // Convert from UI models to workflow models
                            var stageDefinitions = stageModels.Select(s => (
                                stageName: s.Name,
                                currentPercentage: s.CurrentPercentage,
                                targetPercentage: s.TargetPercentage,
                                waitDuration: (TimeSpan?)TimeSpan.FromHours(s.WaitHours)
                            )).ToList();

                            // Initialize the archive configuration
                            workflow.InitializeArchiveConfiguration(stageDefinitions);

                            Console.WriteLine($"Initialized archive workflow with {stageModels.Count} stages");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing stage configuration: {ex.Message}");
                        // Continue with default configuration - the workflow will create its own
                    }
                }

                return CreatedAtAction(nameof(GetWorkflow), new { projectId, workflowId = workflow.Id }, workflow);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet("{projectId}/workflows/{workflowId}")]
        public ActionResult<ConfigCleanupContext> GetWorkflow(Guid projectId, Guid workflowId)
        {
            var workflow = _projectService.GetWorkflowById(workflowId);
            if (workflow == null || workflow.ProjectId != projectId)
                return NotFound();
            return Ok(workflow);
        }

        [HttpPut("{projectId}/workflows/{workflowId}")]
        public ActionResult<ConfigCleanupContext> UpdateWorkflow(Guid projectId, Guid workflowId, [FromBody] ConfigCleanupContext workflow)
        {
            var existingWorkflow = _projectService.GetWorkflowById(workflowId);
            if (existingWorkflow == null || existingWorkflow.ProjectId != projectId)
                return NotFound();

            workflow.Id = workflowId;
            workflow.ProjectId = projectId;
            _projectService.UpdateWorkflow(workflow);
            return Ok(workflow);
        }

        [HttpGet("workflows")]
        public ActionResult<List<ConfigCleanupContext>> GetAllWorkflows()
        {
            var workflows = _projectService.GetAllWorkflows();
            var response = workflows.Select(workflow => new
            {
                Id = workflow.Id,
                ConfigurationName = workflow.ConfigurationName,
                WorkflowType = workflow.WorkflowType,
                CurrentState = workflow.IsCompleted ? "Completed" : "Created",
                Status = workflow.IsCompleted ? "Completed" : "Not Started",
                CreatedAt = workflow.CreatedAt,
                LastUpdated = (DateTime?)null,
                ErrorMessage = workflow.ErrorMessage,
                ProjectId = workflow.ProjectId,
                ProjectName = GetProjectName(workflow.ProjectId),
                Progress = new
                {
                    PercentComplete = workflow.IsCompleted ? 100 : 0,
                    CurrentStep = 1,
                    TotalSteps = 1,
                    CurrentStepDescription = workflow.IsCompleted ? "Completed" : "Not Started",
                    RequiresManualAction = false,
                    ManualActionDescription = ""
                },
                Metadata = new Dictionary<string, string>()
            }).ToList();

            return Ok(response);
        }

        [HttpGet("workflows/{workflowId}")]
        public ActionResult<object> GetWorkflowById(Guid workflowId)
        {
            var workflow = _projectService.GetWorkflowById(workflowId);
            if (workflow == null)
                return NotFound();

            // Build the metadata dictionary
            var metadata = new Dictionary<string, string>();

            // Add stage progress for ArchiveOnly workflows
            if (workflow.WorkflowType == WorkflowType.ArchiveOnly && workflow.ArchiveConfiguration != null)
            {
                try
                {
                    Console.WriteLine($"Found ArchiveOnly workflow with {workflow.ArchiveConfiguration.Stages.Count} stages");

                    var stageProgressModels = workflow.ArchiveConfiguration.Stages.Select(s => new
                    {
                        Name = s.Name,
                        Status = s.Status.ToString(),
                        CurrentAllocationPercentage = s.CurrentAllocationPercentage,
                        TargetAllocationPercentage = s.TargetAllocationPercentage,
                        StartedAt = s.StartedAt,
                        CompletedAt = s.CompletedAt,
                        WaitStartTime = s.WaitStartTime,
                        WaitDuration = s.WaitDuration,
                        ErrorMessage = s.ErrorMessage
                    }).ToList();

                    var stageProgressJson = System.Text.Json.JsonSerializer.Serialize(stageProgressModels);
                    metadata["stageProgress"] = stageProgressJson;

                    Console.WriteLine($"Added stageProgress to metadata: {stageProgressJson}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating stage progress: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Workflow type: {workflow.WorkflowType}, ArchiveConfiguration is null: {workflow.ArchiveConfiguration == null}");
            }

            // Convert ConfigCleanupContext to a response that matches WorkflowResponse structure
            var response = new
            {
                Id = workflow.Id,
                ConfigurationName = workflow.ConfigurationName,
                WorkflowType = workflow.WorkflowType,
                CurrentState = workflow.IsCompleted ? "Completed" : "Created",
                Status = workflow.GetCurrentStatusDescription(),
                CreatedAt = workflow.CreatedAt,
                LastUpdated = (DateTime?)null,
                ErrorMessage = workflow.ErrorMessage,
                ProjectId = workflow.ProjectId,
                ProjectName = GetProjectName(workflow.ProjectId),
                Progress = new
                {
                    PercentComplete = workflow.GetOverallProgress(),
                    CurrentStep = workflow.ArchiveConfiguration?.CurrentStageIndex + 1 ?? 1,
                    TotalSteps = workflow.ArchiveConfiguration?.Stages.Count ?? 1,
                    CurrentStepDescription = workflow.GetCurrentStatusDescription(),
                    RequiresManualAction = false,
                    ManualActionDescription = ""
                },
                Metadata = metadata
            };

            Console.WriteLine($"Returning response with {metadata.Count} metadata items");
            return Ok(response);
        }

        [HttpGet("workflows/summary")]
        public ActionResult<object> GetProjectWorkflowsSummary()
        {
            var allWorkflows = _projectService.GetAllWorkflows();

            var summary = new
            {
                TotalWorkflows = allWorkflows.Count,
                ActiveWorkflows = allWorkflows.Count(w => !w.IsCompleted),
                CompletedWorkflows = allWorkflows.Count(w => w.IsCompleted),
                FailedWorkflows = allWorkflows.Count(w => !string.IsNullOrEmpty(w.ErrorMessage)),
                AwaitingManualAction = 0, // For now, since we don't have this logic in ConfigCleanupContext
                ByType = allWorkflows
                    .GroupBy(w => w.WorkflowType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByState = allWorkflows
                    .GroupBy(w => w.IsCompleted ? "Completed" : "Created") // Simple mapping for now
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            return Ok(summary);
        }

        [HttpPost("workflows/{workflowId}/start")]
        public async Task<ActionResult<object>> StartWorkflow(Guid workflowId)
        {
            try
            {
                var workflow = _projectService.GetWorkflowById(workflowId);
                if (workflow == null)
                    return NotFound($"Workflow with ID {workflowId} not found");

                // Create and start the workflow engine
                if (workflow.WorkflowType == WorkflowType.ArchiveOnly)
                {
                    var archiveWorkflow = new ArchiveOnlyWorkflow(workflow);

                    // Check if workflow can be started
                    if (await archiveWorkflow.CanStartAsync())
                    {
                        // Start the workflow
                        await archiveWorkflow.StartAsync();

                        Console.WriteLine($"Started archive workflow for {workflow.ConfigurationName}");
                    }
                    else
                    {
                        return BadRequest("Workflow cannot be started - check configuration and traffic percentages");
                    }
                }
                else
                {
                    return BadRequest($"Workflow type {workflow.WorkflowType} is not supported yet");
                }

                // Return the updated workflow state
                return GetWorkflowById(workflowId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting workflow: {ex.Message}");
                return StatusCode(500, $"Error starting workflow: {ex.Message}");
            }
        }

        private string? GetProjectName(Guid? projectId)
        {
            if (!projectId.HasValue) return null;
            var project = _projectService.GetProjectById(projectId.Value);
            return project?.Name;
        }
    }
}
