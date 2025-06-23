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

                // Handle workflow type-specific initialization
                switch (request.WorkflowType)
                {
                    case WorkflowType.ArchiveOnly:
                        InitializeArchiveOnlyWorkflow(workflow, request);
                        break;

                    case WorkflowType.TransformToDefault:
                        InitializeTransformToDefaultWorkflow(workflow, request);
                        break;

                    case WorkflowType.CodeFirst:
                        // CodeFirst workflow initialization can be added here later
                        break;
                }

                return CreatedAtAction(nameof(GetWorkflow), new { projectId, workflowId = workflow.Id }, workflow);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
        }

        private void InitializeArchiveOnlyWorkflow(ConfigCleanupContext workflow, CreateWorkflowRequest request)
        {
            if (request.Metadata != null && request.Metadata.ContainsKey("archiveStages"))
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
                            waitDuration: (TimeSpan?)TimeSpan.FromMinutes(1) // Default to 1 minute for demo
                        )).ToList();

                        // Initialize the archive configuration
                        workflow.InitializeArchiveConfiguration(stageDefinitions);

                        Console.WriteLine($"Initialized archive workflow with {stageModels.Count} stages");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing stage configuration: {ex.Message}");
                    // Continue with default configuration
                }
            }
        }

        private void InitializeTransformToDefaultWorkflow(ConfigCleanupContext workflow, CreateWorkflowRequest request)
        {
            // TransformToDefault workflows don't need complex initialization
            // They're ready to run immediately after creation
            Console.WriteLine($"Initialized TransformToDefault workflow for {workflow.ConfigurationName}");

            // If there are any transform-specific metadata in the future, handle them here
            if (request.Metadata != null)
            {
                // Example: transformation rules, target default values, etc.
                foreach (var metadata in request.Metadata)
                {
                    Console.WriteLine($"TransformToDefault metadata - {metadata.Key}: {metadata.Value}");
                }
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
                    PercentComplete = workflow.GetOverallProgress(),
                    CurrentStep = GetCurrentStep(workflow),
                    TotalSteps = GetTotalSteps(workflow),
                    CurrentStepDescription = workflow.GetCurrentStatusDescription(),
                    RequiresManualAction = DeriveCurrentState(workflow) == "AwaitingUserAction",
                    ManualActionDescription = DeriveCurrentState(workflow) == "AwaitingUserAction" ? GetManualActionDescription(workflow) : ""
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

            // Add workflow type-specific metadata
            switch (workflow.WorkflowType)
            {
                case WorkflowType.ArchiveOnly:
                    AddArchiveOnlyMetadata(workflow, metadata);
                    break;

                case WorkflowType.TransformToDefault:
                    AddTransformToDefaultMetadata(workflow, metadata);
                    break;
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
                    CurrentStep = GetCurrentStep(workflow),
                    TotalSteps = GetTotalSteps(workflow),
                    CurrentStepDescription = workflow.GetCurrentStatusDescription(),
                    RequiresManualAction = DeriveRequiresManualAction(workflow),
                    ManualActionDescription = GetManualActionDescription(workflow)
                },
                Metadata = metadata
            };

            Console.WriteLine($"Returning response with {metadata.Count} metadata items");
            return Ok(response);
        }

        private void AddArchiveOnlyMetadata(ConfigCleanupContext workflow, Dictionary<string, string> metadata)
        {
            if (workflow.ArchiveConfiguration != null)
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
        }

        private void AddTransformToDefaultMetadata(ConfigCleanupContext workflow, Dictionary<string, string> metadata)
        {
            // Add transform-specific metadata
            metadata["workflowType"] = "TransformToDefault";
            metadata["isImmediate"] = "true";

            // If there are transformation details stored in the workflow context, add them
            if (!string.IsNullOrEmpty(workflow.ErrorMessage))
            {
                metadata["lastError"] = workflow.ErrorMessage;
            }

            Console.WriteLine("Added TransformToDefault metadata");
        }

        [HttpGet("workflows/summary")]
        public ActionResult<object> GetProjectWorkflowsSummary()
        {
            var allWorkflows = _projectService.GetAllWorkflows();

            var summary = new
            {
                TotalWorkflows = allWorkflows.Count,
                ActiveWorkflows = allWorkflows.Count(w => {
                    var state = DeriveCurrentState(w);
                    return state == "InProgress" || state == "Waiting" || state == "AwaitingUserAction";
                }),
                CompletedWorkflows = allWorkflows.Count(w => w.IsCompleted),
                FailedWorkflows = allWorkflows.Count(w => !string.IsNullOrEmpty(w.ErrorMessage)),
                AwaitingManualAction = allWorkflows.Count(w => DeriveCurrentState(w) == "AwaitingUserAction"),
                ByType = allWorkflows
                    .GroupBy(w => w.WorkflowType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByState = allWorkflows
                    .GroupBy(w => DeriveCurrentState(w))
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

                Console.WriteLine($"Found workflow: {workflow.ConfigurationName}, Type: {workflow.WorkflowType}");

                // Create and start the appropriate workflow engine
                switch (workflow.WorkflowType)
                {
                    case WorkflowType.ArchiveOnly:
                        await StartArchiveOnlyWorkflow(workflow);
                        break;

                    case WorkflowType.TransformToDefault:
                        await StartTransformToDefaultWorkflow(workflow);
                        break;

                    case WorkflowType.CodeFirst:
                        return BadRequest($"Workflow type {workflow.WorkflowType} is not implemented yet");

                    default:
                        return BadRequest($"Unknown workflow type {workflow.WorkflowType}");
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

        private async Task StartArchiveOnlyWorkflow(ConfigCleanupContext workflow)
        {
            var archiveWorkflow = new ArchiveOnlyWorkflow(workflow);
            Console.WriteLine($"Created ArchiveOnlyWorkflow, current state: {archiveWorkflow.CurrentState}");

            if (await archiveWorkflow.CanStartAsync())
            {
                Console.WriteLine("CanStartAsync returned true, calling StartAsync...");
                await archiveWorkflow.StartAsync();
                Console.WriteLine($"StartAsync completed, new state: {archiveWorkflow.CurrentState}");
            }
            else
            {
                Console.WriteLine("CanStartAsync returned false");
                throw new InvalidOperationException("Archive workflow cannot be started - check configuration and traffic percentages");
            }
        }

        private async Task StartTransformToDefaultWorkflow(ConfigCleanupContext workflow)
        {
            var transformWorkflow = new TransformToDefaultWorkflow(workflow);
            Console.WriteLine($"Created TransformToDefaultWorkflow, current state: {transformWorkflow.CurrentState}");

            if (await transformWorkflow.CanStartAsync())
            {
                Console.WriteLine("CanStartAsync returned true, calling StartAsync...");
                await transformWorkflow.StartAsync();
                Console.WriteLine($"StartAsync completed, new state: {transformWorkflow.CurrentState}");
            }
            else
            {
                Console.WriteLine("CanStartAsync returned false");
                throw new InvalidOperationException("Transform workflow cannot be started - check configuration");
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

                Console.WriteLine($"Found workflow: {workflow.ConfigurationName}, Type: {workflow.WorkflowType}");

                // Handle manual progression based on workflow type
                switch (workflow.WorkflowType)
                {
                    case WorkflowType.ArchiveOnly:
                        await ProceedArchiveOnlyWorkflow(workflow);
                        break;

                    case WorkflowType.TransformToDefault:
                        return BadRequest("TransformToDefault workflows don't support manual progression - they complete automatically");

                    case WorkflowType.CodeFirst:
                        return BadRequest($"Manual progression not implemented for workflow type {workflow.WorkflowType}");

                    default:
                        return BadRequest($"Unknown workflow type {workflow.WorkflowType}");
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

        private async Task ProceedArchiveOnlyWorkflow(ConfigCleanupContext workflow)
        {
            var archiveWorkflow = new ArchiveOnlyWorkflow(workflow);
            Console.WriteLine($"Created ArchiveOnlyWorkflow, current state: {archiveWorkflow.CurrentState}");

            if (archiveWorkflow.CanProceed())
            {
                Console.WriteLine($"CanProceed returned true, calling ProceedAsync...");
                Console.WriteLine($"Action description: {archiveWorkflow.GetCurrentActionDescription()}");

                await archiveWorkflow.ProceedAsync();

                Console.WriteLine($"ProceedAsync completed, new state: {archiveWorkflow.CurrentState}");
            }
            else
            {
                Console.WriteLine($"CanProceed returned false - current state: {archiveWorkflow.CurrentState}");
                throw new InvalidOperationException($"Workflow cannot proceed - current state: {archiveWorkflow.CurrentState}");
            }
        }

        // Helper methods for workflow-agnostic operations
        private int GetCurrentStep(ConfigCleanupContext workflow)
        {
            return workflow.WorkflowType switch
            {
                WorkflowType.ArchiveOnly => workflow.ArchiveConfiguration?.CurrentStageIndex + 1 ?? 1,
                WorkflowType.TransformToDefault => workflow.IsCompleted ? 1 : 1,
                _ => 1
            };
        }

        private int GetTotalSteps(ConfigCleanupContext workflow)
        {
            return workflow.WorkflowType switch
            {
                WorkflowType.ArchiveOnly => workflow.ArchiveConfiguration?.Stages.Count ?? 1,
                WorkflowType.TransformToDefault => 1,
                _ => 1
            };
        }

        private bool DeriveRequiresManualAction(ConfigCleanupContext workflow)
        {
            return workflow.WorkflowType switch
            {
                WorkflowType.ArchiveOnly => DeriveCurrentState(workflow) == "AwaitingUserAction",
                WorkflowType.TransformToDefault => !workflow.IsCompleted && string.IsNullOrEmpty(workflow.ErrorMessage),
                _ => false
            };
        }

        private string? GetProjectName(Guid? projectId)
        {
            if (!projectId.HasValue) return null;
            var project = _projectService.GetProjectById(projectId.Value);
            return project?.Name;
        }

        private string DeriveCurrentState(ConfigCleanupContext workflow)
        {
            Console.WriteLine($"=== DeriveCurrentState Debug for {workflow.WorkflowType} ===");
            Console.WriteLine($"IsCompleted: {workflow.IsCompleted}");
            Console.WriteLine($"ErrorMessage: {workflow.ErrorMessage}");

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

            // Handle workflow type-specific state derivation
            return workflow.WorkflowType switch
            {
                WorkflowType.ArchiveOnly => DeriveArchiveOnlyState(workflow),
                WorkflowType.TransformToDefault => DeriveTransformToDefaultState(workflow),
                _ => "Created"
            };
        }

        private string DeriveArchiveOnlyState(ConfigCleanupContext workflow)
        {
            if (workflow.ArchiveConfiguration == null)
            {
                Console.WriteLine("Returning: Created (no config)");
                return "Created";
            }

            // Check if workflow has been started
            if (!workflow.ArchiveConfiguration.WorkflowStartedAt.HasValue)
            {
                Console.WriteLine("Returning: Created (not started)");
                return "Created";
            }

            var currentStage = workflow.ArchiveConfiguration.CurrentStage;
            if (currentStage == null)
            {
                Console.WriteLine("Returning: Completed (no current stage)");
                return "Completed";
            }

            Console.WriteLine($"CurrentStage.Status: {currentStage.Status}");

            var result = currentStage.Status switch
            {
                WorkflowStageStatus.Pending => "AwaitingUserAction",
                WorkflowStageStatus.ReducingTraffic => "InProgress",
                WorkflowStageStatus.Waiting => DeriveWaitingState(currentStage),
                WorkflowStageStatus.Completed => "AwaitingUserAction",
                WorkflowStageStatus.Failed => "Failed",
                _ => "Created"
            };

            Console.WriteLine($"Final result: {result}");
            return result;
        }

        private string DeriveTransformToDefaultState(ConfigCleanupContext workflow)
        {
            // TransformToDefault workflows are simple:
            // - Created: Just created, ready to start
            // - InProgress: Currently transforming (very brief)
            // - Completed: Done
            // - Failed: Error occurred

            // Since TransformToDefault is immediate, if it's not completed and has no error,
            // it's ready to start
            if (!workflow.IsCompleted && string.IsNullOrEmpty(workflow.ErrorMessage))
            {
                Console.WriteLine("Returning: AwaitingUserAction (ready to transform)");
                return "AwaitingUserAction";
            }

            Console.WriteLine("Returning: Created (default for TransformToDefault)");
            return "Created";
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

        private string GetManualActionDescription(ConfigCleanupContext workflow)
        {
            return workflow.WorkflowType switch
            {
                WorkflowType.ArchiveOnly => GetArchiveOnlyActionDescription(workflow),
                WorkflowType.TransformToDefault => GetTransformToDefaultActionDescription(workflow),
                _ => ""
            };
        }

        private string GetArchiveOnlyActionDescription(ConfigCleanupContext workflow)
        {
            if (workflow.ArchiveConfiguration == null) return "";

            var currentStage = workflow.ArchiveConfiguration.CurrentStage;
            if (currentStage == null) return "Ready to Archive";

            return currentStage.Status switch
            {
                WorkflowStageStatus.Pending when currentStage.CurrentAllocationPercentage > currentStage.TargetAllocationPercentage =>
                    $"Click to start rolldown from {currentStage.CurrentAllocationPercentage}% to {currentStage.TargetAllocationPercentage}%",
                WorkflowStageStatus.Waiting when currentStage.CurrentAllocationPercentage > currentStage.TargetAllocationPercentage =>
                    $"Click to continue rolldown from {currentStage.CurrentAllocationPercentage}% to {currentStage.TargetAllocationPercentage}%",
                _ => "Click to proceed to next step"
            };
        }

        private string GetTransformToDefaultActionDescription(ConfigCleanupContext workflow)
        {
            if (workflow.IsCompleted)
                return "";

            if (!string.IsNullOrEmpty(workflow.ErrorMessage))
                return "Transform failed - check error details";

            return $"Click to transform '{workflow.ConfigurationName}' to default values";
        }
    }
}