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
                                // waitDuration: (TimeSpan?)TimeSpan.FromHours(s.WaitHours)
                                waitDuration: (TimeSpan?)TimeSpan.FromMinutes(1) // Default to 1 minute for simplicity, can be adjusted
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
                CurrentState = DeriveCurrentState(workflow),
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
                CurrentState = DeriveCurrentState(workflow),
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
                Console.WriteLine($"=== Starting workflow {workflowId} ===");

                var workflow = _projectService.GetWorkflowById(workflowId);
                if (workflow == null)
                {
                    Console.WriteLine($"Workflow {workflowId} not found");
                    return NotFound($"Workflow with ID {workflowId} not found");
                }

                Console.WriteLine($"Found workflow: {workflow.ConfigurationName}");
                Console.WriteLine($"Current IsCompleted: {workflow.IsCompleted}");
                Console.WriteLine($"ArchiveConfiguration is null: {workflow.ArchiveConfiguration == null}");

                if (workflow.ArchiveConfiguration != null)
                {
                    Console.WriteLine($"WorkflowStartedAt before start: {workflow.ArchiveConfiguration.WorkflowStartedAt}");
                    Console.WriteLine($"Current stage: {workflow.ArchiveConfiguration.CurrentStage?.Name}");
                    Console.WriteLine($"Current stage status: {workflow.ArchiveConfiguration.CurrentStage?.Status}");
                }

                // Create and start the workflow engine
                if (workflow.WorkflowType == WorkflowType.ArchiveOnly)
                {
                    var archiveWorkflow = new ArchiveOnlyWorkflow(workflow);
                    Console.WriteLine($"Created ArchiveOnlyWorkflow, current state: {archiveWorkflow.CurrentState}");

                    // Check if workflow can be started
                    if (await archiveWorkflow.CanStartAsync())
                    {
                        Console.WriteLine("CanStartAsync returned true, calling StartAsync...");

                        // Start the workflow
                        await archiveWorkflow.StartAsync();

                        Console.WriteLine($"StartAsync completed, new state: {archiveWorkflow.CurrentState}");
                        Console.WriteLine($"WorkflowStartedAt after start: {workflow.ArchiveConfiguration?.WorkflowStartedAt}");
                        Console.WriteLine($"Current stage status after start: {workflow.ArchiveConfiguration?.CurrentStage?.Status}");
                    }
                    else
                    {
                        Console.WriteLine("CanStartAsync returned false");
                        return BadRequest("Workflow cannot be started - check configuration and traffic percentages");
                    }
                }
                else
                {
                    return BadRequest($"Workflow type {workflow.WorkflowType} is not supported yet");
                }

                Console.WriteLine("=== Workflow start completed, returning response ===");

                // Return the updated workflow state
                return GetWorkflowById(workflowId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting workflow: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, $"Error starting workflow: {ex.Message}");
            }
        }

        [HttpPost("{projectId}/workflows/{workflowId}/proceed")]
        public async Task<ActionResult<object>> ProceedWorkflow(Guid projectId, Guid workflowId)
        {
            try
            {
                Console.WriteLine($"=== Proceeding workflow {workflowId} in project {projectId} ===");

                var workflow = _projectService.GetWorkflowById(workflowId);
                if (workflow == null || workflow.ProjectId != projectId)
                {
                    Console.WriteLine($"Workflow {workflowId} not found in project {projectId}");
                    return NotFound($"Workflow with ID {workflowId} not found in project {projectId}");
                }

                Console.WriteLine($"Found workflow: {workflow.ConfigurationName}");
                Console.WriteLine($"Current derived state: {DeriveCurrentState(workflow)}");

                // Handle manual progression based on workflow type
                if (workflow.WorkflowType == WorkflowType.ArchiveOnly)
                {
                    var archiveWorkflow = new ArchiveOnlyWorkflow(workflow);
                    Console.WriteLine($"Created ArchiveOnlyWorkflow, current state: {archiveWorkflow.CurrentState}");

                    // Check if workflow can proceed
                    if (archiveWorkflow.CanProceed())
                    {
                        Console.WriteLine($"CanProceed returned true, calling ProceedAsync...");
                        Console.WriteLine($"Action description: {archiveWorkflow.GetCurrentActionDescription()}");

                        // Proceed with the workflow
                        await archiveWorkflow.ProceedAsync();

                        Console.WriteLine($"ProceedAsync completed, new state: {archiveWorkflow.CurrentState}");
                        Console.WriteLine($"New derived state: {DeriveCurrentState(workflow)}");
                    }
                    else
                    {
                        Console.WriteLine($"CanProceed returned false - current state: {archiveWorkflow.CurrentState}");
                        return BadRequest($"Workflow cannot proceed - current state: {archiveWorkflow.CurrentState}");
                    }
                }
                else
                {
                    return BadRequest($"Manual progression not supported for workflow type {workflow.WorkflowType}");
                }

                Console.WriteLine("=== Workflow proceed completed, returning response ===");

                // Return the updated workflow state
                return GetWorkflowById(workflowId);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Invalid operation: {ex.Message}");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error proceeding workflow: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, $"Error proceeding workflow: {ex.Message}");
            }
        }

        private string? GetProjectName(Guid? projectId)
        {
            if (!projectId.HasValue) return null;
            var project = _projectService.GetProjectById(projectId.Value);
            return project?.Name;
        }

        private string DeriveCurrentState(ConfigCleanupContext workflow)
        {
            Console.WriteLine($"=== DeriveCurrentState Debug ===");
            Console.WriteLine($"IsCompleted: {workflow.IsCompleted}");
            Console.WriteLine($"ErrorMessage: {workflow.ErrorMessage}");
            Console.WriteLine($"ArchiveConfiguration is null: {workflow.ArchiveConfiguration == null}");

            if (workflow.IsCompleted)
            {
                Console.WriteLine("Returning: Completed");
                return "Completed";
            }

            if (!string.IsNullOrEmpty(workflow.ErrorMessage))
            {
                Console.WriteLine("Returning: Failed");
                return "Failed";
            }

            if (workflow.ArchiveConfiguration == null)
            {
                Console.WriteLine("Returning: Created (no config)");
                return "Created";
            }

            Console.WriteLine($"WorkflowStartedAt: {workflow.ArchiveConfiguration.WorkflowStartedAt}");
            Console.WriteLine($"WorkflowStartedAt.HasValue: {workflow.ArchiveConfiguration.WorkflowStartedAt.HasValue}");

            // Check if workflow has been started
            if (!workflow.ArchiveConfiguration.WorkflowStartedAt.HasValue)
            {
                Console.WriteLine("Returning: Created (not started)");
                return "Created";
            }

            var currentStage = workflow.ArchiveConfiguration.CurrentStage;
            Console.WriteLine($"CurrentStage is null: {currentStage == null}");

            if (currentStage == null)
            {
                Console.WriteLine("Returning: Completed (no current stage)");
                return "Completed"; // No more stages
            }

            Console.WriteLine($"CurrentStage.Name: {currentStage.Name}");
            Console.WriteLine($"CurrentStage.Status: {currentStage.Status}");

            // Determine state based on current stage status
            var result = currentStage.Status switch
            {
                WorkflowStageStatus.Pending => "AwaitingUserAction", // Ready to start this stage
                WorkflowStageStatus.ReducingTraffic => "InProgress", // Currently reducing traffic
                WorkflowStageStatus.Waiting => DeriveWaitingState(currentStage), // Check if still waiting or ready for next action
                WorkflowStageStatus.Completed => "AwaitingUserAction", // Stage done, ready for next action
                WorkflowStageStatus.Failed => "Failed",
                _ => "Created"
            };

            Console.WriteLine($"Final result: {result}");
            Console.WriteLine($"=== End DeriveCurrentState Debug ===");

            return result;
        }

        // Helper method to determine if waiting or ready for user action
        private string DeriveWaitingState(WorkflowStage stage)
        {
            if (!stage.WaitStartTime.HasValue)
                return "Waiting";

            var waitEndTime = stage.WaitStartTime.Value + stage.WaitDuration;
            var now = DateTime.UtcNow;

            if (now >= waitEndTime)
            {
                // Wait period is over, ready for user action
                return "AwaitingUserAction";
            }
            else
            {
                // Still waiting
                return "Waiting";
            }
        }

    }
}
