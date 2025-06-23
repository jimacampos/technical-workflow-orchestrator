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
                    PercentComplete = workflow.GetOverallProgress(), // Use actual progress calculation
                    CurrentStep = workflow.ArchiveConfiguration?.CurrentStageIndex + 1 ?? 1,
                    TotalSteps = workflow.ArchiveConfiguration?.Stages.Count ?? 1,
                    CurrentStepDescription = workflow.GetCurrentStatusDescription(),
                    RequiresManualAction = DeriveCurrentState(workflow) == "AwaitingUserAction", // Check if awaiting user action
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

            // Add stage progress for ArchiveOnly workflows
            if (workflow.WorkflowType == WorkflowType.ArchiveOnly && workflow.ArchiveConfiguration != null)
            {
                try
                {
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
                }
                catch (Exception ex)
                {
                    
                }
            }

            // Add CodeFirst metadata
            else if (workflow.WorkflowType == WorkflowType.CodeFirst)
            {
                try
                {

                    if (workflow.CodeWorkStartedAt.HasValue)
                    {
                        metadata["codeWorkStartedAt"] = workflow.CodeWorkStartedAt.Value.ToString("O");
                    }

                    if (workflow.PRCreatedAt.HasValue)
                    {
                        metadata["prCreatedAt"] = workflow.PRCreatedAt.Value.ToString("O");
                    }

                    if (workflow.PRApprovedAt.HasValue)
                    {
                        metadata["prApprovedAt"] = workflow.PRApprovedAt.Value.ToString("O");
                    }

                    if (workflow.PRMergedAt.HasValue)
                    {
                        metadata["prMergedAt"] = workflow.PRMergedAt.Value.ToString("O");
                    }

                    if (workflow.DeploymentDetectedAt.HasValue)
                    {
                        metadata["deploymentDetectedAt"] = workflow.DeploymentDetectedAt.Value.ToString("O");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating CodeFirst metadata: {ex.Message}");
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
                PullRequestUrl = workflow.PullRequestUrl, // Include this!
                CodeWorkStartedAt = workflow.CodeWorkStartedAt, // Include these for debugging
                PRCreatedAt = workflow.PRCreatedAt,
                PRApprovedAt = workflow.PRApprovedAt,
                PRMergedAt = workflow.PRMergedAt,
                DeploymentDetectedAt = workflow.DeploymentDetectedAt,
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

            return Ok(response);
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
                AwaitingManualAction = allWorkflows.Count(w => DeriveCurrentState(w) == "AwaitingUserAction"), // Fixed calculation
                ByType = allWorkflows
                    .GroupBy(w => w.WorkflowType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByState = allWorkflows
                    .GroupBy(w => DeriveCurrentState(w)) // Use DeriveCurrentState instead of simple mapping
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
                {
                    return NotFound($"Workflow with ID {workflowId} not found");
                }

                // Handle different workflow types
                if (workflow.WorkflowType == WorkflowType.ArchiveOnly)
                {
                    var archiveWorkflow = new ArchiveOnlyWorkflow(workflow);

                    // Check if workflow can be started
                    if (await archiveWorkflow.CanStartAsync())
                    {
                        // Start the workflow
                        await archiveWorkflow.StartAsync();

                        // Persist the changes
                        _projectService.UpdateWorkflow(workflow);
                    }
                    else
                    {
                        return BadRequest("Workflow cannot be started - check configuration and traffic percentages");
                    }
                }
                else if (workflow.WorkflowType == WorkflowType.TransformToDefault)
                {
                    var transformWorkflow = new TransformToDefaultWorkflow(workflow);

                    // Check if workflow can be started
                    if (await transformWorkflow.CanStartAsync())
                    {
                        // Start the workflow
                        await transformWorkflow.StartAsync();

                        // Persist the changes
                        _projectService.UpdateWorkflow(workflow);
                    }
                    else
                    {
                        return BadRequest("TransformToDefault workflow cannot be started - check configuration");
                    }
                }
                else if (workflow.WorkflowType == WorkflowType.CodeFirst)
                {

                    var codeFirstWorkflow = new CodeFirstWorkflow(workflow);

                    // Check if workflow can be started
                    if (await codeFirstWorkflow.CanStartAsync())
                    {
                        // Start the workflow
                        await codeFirstWorkflow.StartAsync();

                        // Persist the changes
                        _projectService.UpdateWorkflow(workflow);
                    }
                    else
                    {
                        Console.WriteLine("CodeFirst CanStartAsync returned false");
                        return BadRequest("CodeFirst workflow cannot be started - check configuration");
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
                return StatusCode(500, $"Error starting workflow: {ex.Message}");
            }
        }

        [HttpPost("{projectId}/workflows/{workflowId}/proceed")]
        public async Task<ActionResult<object>> ProceedWorkflow(Guid projectId, Guid workflowId)
        {
            try
            {
                var workflow = _projectService.GetWorkflowById(workflowId);
                if (workflow == null || workflow.ProjectId != projectId)
                {
                    return NotFound($"Workflow with ID {workflowId} not found in project {projectId}");
                }

                // Handle manual progression based on workflow type
                if (workflow.WorkflowType == WorkflowType.ArchiveOnly)
                {
                    var archiveWorkflow = new ArchiveOnlyWorkflow(workflow);

                    // Check if workflow can proceed
                    if (archiveWorkflow.CanProceed())
                    {
                        // Proceed with the workflow
                        await archiveWorkflow.ProceedAsync();

                        // Persist the changes
                        _projectService.UpdateWorkflow(workflow);
                    }
                    else
                    {
                        return BadRequest($"Workflow cannot proceed - current state: {archiveWorkflow.CurrentState}");
                    }
                }
                else if (workflow.WorkflowType == WorkflowType.TransformToDefault)
                {
                    var transformWorkflow = new TransformToDefaultWorkflow(workflow);

                    // Check if workflow can proceed
                    if (transformWorkflow.CanProceed())
                    {
                        // Proceed with the workflow
                        await transformWorkflow.ProceedAsync();

                        // Persist the changes
                        _projectService.UpdateWorkflow(workflow);
                    }
                    else
                    {
                        return BadRequest($"TransformToDefault workflow cannot proceed - current state: {transformWorkflow.CurrentState}");
                    }
                }
                else if (workflow.WorkflowType == WorkflowType.CodeFirst)
                {
                    var codeFirstWorkflow = new CodeFirstWorkflow(workflow);

                    // Determine which action to take based on current state
                    var currentAction = GetCodeFirstCurrentAction(workflow);

                    try
                    {
                        switch (currentAction)
                        {
                            case "CompleteCodeWork":
                                await codeFirstWorkflow.CompleteCodeWorkAsync();
                                break;

                            case "MarkPRApproved":
                                await codeFirstWorkflow.NotifyPRApprovedAsync();
                                workflow.PRApprovedAt = DateTime.UtcNow;
                                break;

                            case "ConfirmMerge":
                                await codeFirstWorkflow.ConfirmMergeAsync();
                                workflow.PRMergedAt = DateTime.UtcNow;
                                break;

                            case "ConfirmDeployment":
                                await codeFirstWorkflow.NotifyDeploymentCompletedAsync();
                                workflow.DeploymentDetectedAt = DateTime.UtcNow;
                                workflow.IsCompleted = true;
                                break;

                            default:
                                return BadRequest($"CodeFirst workflow cannot proceed - unknown action: {currentAction}");
                        }

                        // Persist the changes
                        _projectService.UpdateWorkflow(workflow);
                    }
                    catch (Exception ex)
                    {
                        return BadRequest($"Error proceeding CodeFirst workflow: {ex.Message}");
                    }
                }

                // Return the updated workflow state
                return GetWorkflowById(workflowId);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error proceeding workflow: {ex.Message}");
            }
        }

        [HttpPut("{projectId}/workflows/{workflowId}/pullrequest")]
        public async Task<ActionResult<object>> SetPullRequestUrl(Guid projectId, Guid workflowId, [FromBody] SetPullRequestRequest request)
        {
            try
            {
                var workflow = _projectService.GetWorkflowById(workflowId);
                if (workflow == null || workflow.ProjectId != projectId)
                {
                    return NotFound($"Workflow with ID {workflowId} not found in project {projectId}");
                }

                if (workflow.WorkflowType != WorkflowType.CodeFirst)
                {
                    return BadRequest("Setting PR URL is only supported for CodeFirst workflows");
                }

                var codeFirstWorkflow = new CodeFirstWorkflow(workflow);

                // Validate that we're in the correct state to set PR URL
                if (!workflow.CodeWorkStartedAt.HasValue)
                {
                    return BadRequest("Code work must be completed before setting PR URL");
                }

                // Set the PR URL in the context FIRST
                workflow.PullRequestUrl = request.PullRequestUrl;
                workflow.PRCreatedAt = DateTime.UtcNow;

                // Then trigger the state machine
                await codeFirstWorkflow.SetPullRequestAsync(request.PullRequestUrl);

                // Persist the changes
                _projectService.UpdateWorkflow(workflow);

                // Return the updated workflow state
                return GetWorkflowById(workflowId);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error setting PR URL: {ex.Message}");
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

            if (workflow.IsCompleted)
            {
                return "Completed";
            }

            if (!string.IsNullOrEmpty(workflow.ErrorMessage))
            {
                return "Failed";
            }

            // Handle different workflow types
            if (workflow.WorkflowType == WorkflowType.ArchiveOnly)
            {
                return DeriveArchiveOnlyState(workflow);
            }
            else if (workflow.WorkflowType == WorkflowType.TransformToDefault)
            {
                return DeriveTransformToDefaultState(workflow);
            }
            else if (workflow.WorkflowType == WorkflowType.CodeFirst)
            {
                return DeriveCodeFirstState(workflow);
            }

            return "Created";
        }


        private string DeriveCodeFirstState(ConfigCleanupContext workflow)
        {
            // For CodeFirst workflows, determine state based on progress timestamps
            if (!workflow.CodeWorkStartedAt.HasValue)
            {
                return "Created";
            }

            if (!workflow.PRCreatedAt.HasValue && string.IsNullOrEmpty(workflow.PullRequestUrl))
            {
                return "AwaitingUserAction";
            }

            if (!workflow.PRApprovedAt.HasValue)
            {
                return "AwaitingUserAction"; // This will show the Proceed button
            }

            if (!workflow.PRMergedAt.HasValue)
            {
                return "AwaitingUserAction";
            }

            if (!workflow.DeploymentDetectedAt.HasValue)
            {
                return "AwaitingUserAction";
            }

            return "Completed";
        }

        private string GetCodeFirstManualActionDescription(ConfigCleanupContext workflow)
        {
            if (workflow.IsCompleted || !string.IsNullOrEmpty(workflow.ErrorMessage))
                return "";

            if (!workflow.CodeWorkStartedAt.HasValue)
                return "Click to start working on code changes";

            if (!workflow.PRCreatedAt.HasValue)
                return "Click to create/link pull request";

            if (!workflow.PRApprovedAt.HasValue)
                return $"Waiting for PR approval: {workflow.PullRequestUrl}";

            if (!workflow.PRMergedAt.HasValue)
                return "Click to confirm PR has been merged";

            if (!workflow.DeploymentDetectedAt.HasValue)
                return "Click to confirm deployment is complete";

            return "";
        }

        private string DeriveArchiveOnlyState(ConfigCleanupContext workflow)
        {
            if (workflow.ArchiveConfiguration == null)
            {
                return "Created";
            }

            // Check if workflow has been started
            if (!workflow.ArchiveConfiguration.WorkflowStartedAt.HasValue)
            {
                return "Created";
            }

            var currentStage = workflow.ArchiveConfiguration.CurrentStage;

            if (currentStage == null)
            {
                return "Completed"; // No more stages
            }

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

            return result;
        }

        private string DeriveTransformToDefaultState(ConfigCleanupContext workflow)
        {
            // For TransformToDefault workflows:
            // - If not started (TransformStartedAt is null), show as "Created" -> UI shows "Start" button
            // - If started but not completed, show as "AwaitingUserAction" -> UI shows "Proceed" button

            if (!workflow.IsCompleted && string.IsNullOrEmpty(workflow.ErrorMessage))
            {
                if (workflow.TransformStartedAt.HasValue)
                {
                    return "AwaitingUserAction";
                }
                else
                {
                    return "Created";
                }
            }

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
            if (workflow.WorkflowType == WorkflowType.ArchiveOnly)
            {
                if (workflow.ArchiveConfiguration == null) return "";

                var currentStage = workflow.ArchiveConfiguration.CurrentStage;
                if (currentStage == null) return "Ready to Archive";

                // Determine what action is needed based on stage status
                return currentStage.Status switch
                {
                    WorkflowStageStatus.Pending when currentStage.CurrentAllocationPercentage > currentStage.TargetAllocationPercentage =>
                        $"Click to start rolldown from {currentStage.CurrentAllocationPercentage}% to {currentStage.TargetAllocationPercentage}%",
                    WorkflowStageStatus.Waiting when currentStage.CurrentAllocationPercentage > currentStage.TargetAllocationPercentage =>
                        $"Click to continue rolldown from {currentStage.CurrentAllocationPercentage}% to {currentStage.TargetAllocationPercentage}%",
                    _ => "Click to proceed to next step"
                };
            }
            else if (workflow.WorkflowType == WorkflowType.TransformToDefault)
            {
                if (workflow.IsCompleted)
                    return "";

                if (!string.IsNullOrEmpty(workflow.ErrorMessage))
                    return "Transform failed - check error details";

                // Check if workflow has been started
                if (workflow.TransformStartedAt.HasValue)
                {
                    return $"Click to transform '{workflow.ConfigurationName}' to default values";
                }
                else
                {
                    return $"Click to start transformation of '{workflow.ConfigurationName}' to default values";
                }
            }
            else if (workflow.WorkflowType == WorkflowType.CodeFirst)
            {
                return GetCodeFirstManualActionDescription(workflow);
            }

            return "";
        }

        private string GetCodeFirstCurrentAction(ConfigCleanupContext workflow)
        {
            if (!workflow.CodeWorkStartedAt.HasValue)
            {
                return "StartCodeWork";
            }

            if (!workflow.PRCreatedAt.HasValue)
            {
                return "CreatePR"; // This will need the separate endpoint
            }

            if (!workflow.PRApprovedAt.HasValue)
            {
                return "MarkPRApproved";
            }

            if (!workflow.PRMergedAt.HasValue)
            {
                return "ConfirmMerge";
            }

            if (!workflow.DeploymentDetectedAt.HasValue)
            {
                return "ConfirmDeployment";
            }

            return "Complete";
        }
    }
}
