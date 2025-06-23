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

            // Add CodeFirst metadata
            else if (workflow.WorkflowType == WorkflowType.CodeFirst)
            {
                try
                {
                    Console.WriteLine($"Adding CodeFirst metadata for workflow {workflow.Id}");

                    if (workflow.CodeWorkStartedAt.HasValue)
                    {
                        metadata["codeWorkStartedAt"] = workflow.CodeWorkStartedAt.Value.ToString("O");
                        Console.WriteLine($"Added codeWorkStartedAt: {workflow.CodeWorkStartedAt.Value}");
                    }

                    if (workflow.PRCreatedAt.HasValue)
                    {
                        metadata["prCreatedAt"] = workflow.PRCreatedAt.Value.ToString("O");
                        Console.WriteLine($"Added prCreatedAt: {workflow.PRCreatedAt.Value}");
                    }

                    if (workflow.PRApprovedAt.HasValue)
                    {
                        metadata["prApprovedAt"] = workflow.PRApprovedAt.Value.ToString("O");
                        Console.WriteLine($"Added prApprovedAt: {workflow.PRApprovedAt.Value}");
                    }

                    if (workflow.PRMergedAt.HasValue)
                    {
                        metadata["prMergedAt"] = workflow.PRMergedAt.Value.ToString("O");
                        Console.WriteLine($"Added prMergedAt: {workflow.PRMergedAt.Value}");
                    }

                    if (workflow.DeploymentDetectedAt.HasValue)
                    {
                        metadata["deploymentDetectedAt"] = workflow.DeploymentDetectedAt.Value.ToString("O");
                        Console.WriteLine($"Added deploymentDetectedAt: {workflow.DeploymentDetectedAt.Value}");
                    }

                    Console.WriteLine($"CodeFirst metadata count: {metadata.Count}");
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
                Console.WriteLine($"=== Starting workflow {workflowId} ===");

                var workflow = _projectService.GetWorkflowById(workflowId);
                if (workflow == null)
                {
                    Console.WriteLine($"Workflow {workflowId} not found");
                    return NotFound($"Workflow with ID {workflowId} not found");
                }

                Console.WriteLine($"Found workflow: {workflow.ConfigurationName}, Type: {workflow.WorkflowType}");
                Console.WriteLine($"Current IsCompleted: {workflow.IsCompleted}");

                // Handle different workflow types
                if (workflow.WorkflowType == WorkflowType.ArchiveOnly)
                {
                    Console.WriteLine($"ArchiveConfiguration is null: {workflow.ArchiveConfiguration == null}");

                    if (workflow.ArchiveConfiguration != null)
                    {
                        Console.WriteLine($"WorkflowStartedAt before start: {workflow.ArchiveConfiguration.WorkflowStartedAt}");
                        Console.WriteLine($"Current stage: {workflow.ArchiveConfiguration.CurrentStage?.Name}");
                        Console.WriteLine($"Current stage status: {workflow.ArchiveConfiguration.CurrentStage?.Status}");
                    }

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

                        // Persist the changes
                        _projectService.UpdateWorkflow(workflow);
                        Console.WriteLine("ArchiveOnly workflow changes persisted");
                    }
                    else
                    {
                        Console.WriteLine("CanStartAsync returned false");
                        return BadRequest("Workflow cannot be started - check configuration and traffic percentages");
                    }
                }
                else if (workflow.WorkflowType == WorkflowType.TransformToDefault)
                {
                    Console.WriteLine($"TransformStartedAt before start: {workflow.TransformStartedAt}");

                    var transformWorkflow = new TransformToDefaultWorkflow(workflow);
                    Console.WriteLine($"Created TransformToDefaultWorkflow, current state: {transformWorkflow.CurrentState}");

                    // Check if workflow can be started
                    if (await transformWorkflow.CanStartAsync())
                    {
                        Console.WriteLine("TransformToDefault CanStartAsync returned true, calling StartAsync...");

                        // Start the workflow
                        await transformWorkflow.StartAsync();

                        Console.WriteLine($"TransformToDefault StartAsync completed, new state: {transformWorkflow.CurrentState}");
                        Console.WriteLine($"TransformStartedAt after start: {workflow.TransformStartedAt}");

                        // Persist the changes
                        _projectService.UpdateWorkflow(workflow);
                        Console.WriteLine("TransformToDefault workflow changes persisted");
                    }
                    else
                    {
                        Console.WriteLine("TransformToDefault CanStartAsync returned false");
                        return BadRequest("TransformToDefault workflow cannot be started - check configuration");
                    }
                }
                else if (workflow.WorkflowType == WorkflowType.CodeFirst)
                {
                    Console.WriteLine($"CodeWorkStartedAt before start: {workflow.CodeWorkStartedAt}");

                    var codeFirstWorkflow = new CodeFirstWorkflow(workflow);
                    Console.WriteLine($"Created CodeFirstWorkflow, current state: {codeFirstWorkflow.CurrentState}");

                    // Check if workflow can be started
                    if (await codeFirstWorkflow.CanStartAsync())
                    {
                        Console.WriteLine("CodeFirst CanStartAsync returned true, calling StartAsync...");

                        // Start the workflow
                        await codeFirstWorkflow.StartAsync();

                        Console.WriteLine($"CodeFirst StartAsync completed, new state: {codeFirstWorkflow.CurrentState}");
                        Console.WriteLine($"CodeWorkStartedAt after start: {workflow.CodeWorkStartedAt}");

                        // Persist the changes
                        _projectService.UpdateWorkflow(workflow);
                        Console.WriteLine("CodeFirst workflow changes persisted");
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

                Console.WriteLine($"Found workflow: {workflow.ConfigurationName}, Type: {workflow.WorkflowType}");
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

                        // Persist the changes
                        _projectService.UpdateWorkflow(workflow);
                        Console.WriteLine("ArchiveOnly workflow changes persisted");
                    }
                    else
                    {
                        Console.WriteLine($"CanProceed returned false - current state: {archiveWorkflow.CurrentState}");
                        return BadRequest($"Workflow cannot proceed - current state: {archiveWorkflow.CurrentState}");
                    }
                }

                else if (workflow.WorkflowType == WorkflowType.TransformToDefault)
                {
                    Console.WriteLine($"=== Controller TransformToDefault Proceed Debug ===");
                    Console.WriteLine($"TransformStartedAt before proceed: {workflow.TransformStartedAt}");
                    Console.WriteLine($"IsCompleted: {workflow.IsCompleted}");
                    Console.WriteLine($"ErrorMessage: '{workflow.ErrorMessage}'");

                    var transformWorkflow = new TransformToDefaultWorkflow(workflow);
                    Console.WriteLine($"Created TransformToDefaultWorkflow, current state: {transformWorkflow.CurrentState}");
                    Console.WriteLine($"Expected state for proceed: {WorkflowState.AwaitingUserAction}");

                    // Check if workflow can proceed
                    if (transformWorkflow.CanProceed())
                    {
                        Console.WriteLine($"TransformToDefault CanProceed returned true, calling ProceedAsync...");
                        Console.WriteLine($"Action description: {transformWorkflow.GetCurrentActionDescription()}");

                        // Proceed with the workflow
                        await transformWorkflow.ProceedAsync();

                        Console.WriteLine($"TransformToDefault ProceedAsync completed, new state: {transformWorkflow.CurrentState}");
                        Console.WriteLine($"IsCompleted after proceed: {workflow.IsCompleted}");
                        Console.WriteLine($"New derived state: {DeriveCurrentState(workflow)}");

                        // Persist the changes
                        _projectService.UpdateWorkflow(workflow);
                        Console.WriteLine("TransformToDefault workflow changes persisted");
                    }
                    else
                    {
                        Console.WriteLine($"=== CanProceed returned FALSE - Debug Info ===");
                        Console.WriteLine($"Current state: {transformWorkflow.CurrentState}");
                        Console.WriteLine($"Expected state: {WorkflowState.AwaitingUserAction}");
                        Console.WriteLine($"State comparison: {transformWorkflow.CurrentState} == {WorkflowState.AwaitingUserAction} = {transformWorkflow.CurrentState == WorkflowState.AwaitingUserAction}");
                        Console.WriteLine($"DetermineInitialState would return: {DetermineInitialStateForDebug(workflow)}");
                        Console.WriteLine($"=== End CanProceed Debug ===");

                        return BadRequest($"TransformToDefault workflow cannot proceed - current state: {transformWorkflow.CurrentState}");
                    }
                    Console.WriteLine($"=== End Controller TransformToDefault Proceed Debug ===");
                }
                else if (workflow.WorkflowType == WorkflowType.CodeFirst)
                {
                    Console.WriteLine($"=== Controller CodeFirst Proceed Debug ===");
                    Console.WriteLine($"Workflow ID: {workflow.Id}");
                    Console.WriteLine($"Derived Current State: {DeriveCurrentState(workflow)}");
                    Console.WriteLine($"CodeWorkStartedAt: {workflow.CodeWorkStartedAt}");
                    Console.WriteLine($"PRCreatedAt: {workflow.PRCreatedAt}");
                    Console.WriteLine($"PRApprovedAt: {workflow.PRApprovedAt}");
                    Console.WriteLine($"PRMergedAt: {workflow.PRMergedAt}");
                    Console.WriteLine($"DeploymentDetectedAt: {workflow.DeploymentDetectedAt}");
                    Console.WriteLine($"PullRequestUrl: {workflow.PullRequestUrl}");

                    var codeFirstWorkflow = new CodeFirstWorkflow(workflow);
                    Console.WriteLine($"Created CodeFirstWorkflow, state machine state: {codeFirstWorkflow.CurrentState}");

                    // Determine which action to take based on current state
                    var currentAction = GetCodeFirstCurrentAction(workflow);
                    Console.WriteLine($"Determined current action: {currentAction}");

                    try
                    {
                        switch (currentAction)
                        {
                            case "CompleteCodeWork":
                                Console.WriteLine("Executing CompleteCodeWork...");
                                await codeFirstWorkflow.CompleteCodeWorkAsync();
                                break;

                            case "MarkPRApproved":
                                Console.WriteLine("Executing MarkPRApproved...");
                                await codeFirstWorkflow.NotifyPRApprovedAsync();
                                workflow.PRApprovedAt = DateTime.UtcNow;
                                Console.WriteLine($"Set PRApprovedAt to: {workflow.PRApprovedAt}");
                                break;

                            case "ConfirmMerge":
                                Console.WriteLine("Executing ConfirmMerge...");
                                await codeFirstWorkflow.ConfirmMergeAsync();
                                workflow.PRMergedAt = DateTime.UtcNow;
                                Console.WriteLine($"Set PRMergedAt to: {workflow.PRMergedAt}");
                                break;

                            case "ConfirmDeployment":
                                Console.WriteLine("Executing ConfirmDeployment...");
                                await codeFirstWorkflow.NotifyDeploymentCompletedAsync();
                                workflow.DeploymentDetectedAt = DateTime.UtcNow;
                                workflow.IsCompleted = true;
                                Console.WriteLine($"Set DeploymentDetectedAt to: {workflow.DeploymentDetectedAt}");
                                Console.WriteLine($"Set IsCompleted to: {workflow.IsCompleted}");
                                break;

                            default:
                                Console.WriteLine($"Unknown or invalid action: {currentAction}");
                                return BadRequest($"CodeFirst workflow cannot proceed - unknown action: {currentAction}");
                        }

                        Console.WriteLine($"CodeFirst action {currentAction} completed, new state machine state: {codeFirstWorkflow.CurrentState}");
                        Console.WriteLine($"New derived state: {DeriveCurrentState(workflow)}");

                        // Persist the changes
                        _projectService.UpdateWorkflow(workflow);
                        Console.WriteLine("CodeFirst workflow changes persisted");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during CodeFirst proceed: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        return BadRequest($"Error proceeding CodeFirst workflow: {ex.Message}");
                    }

                    Console.WriteLine($"=== End Controller CodeFirst Proceed Debug ===");
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

        [HttpPut("{projectId}/workflows/{workflowId}/pullrequest")]
        public async Task<ActionResult<object>> SetPullRequestUrl(Guid projectId, Guid workflowId, [FromBody] SetPullRequestRequest request)
        {
            try
            {
                Console.WriteLine($"=== Setting PR URL for workflow {workflowId} ===");
                Console.WriteLine($"Request PR URL: '{request.PullRequestUrl}'");

                var workflow = _projectService.GetWorkflowById(workflowId);
                if (workflow == null || workflow.ProjectId != projectId)
                {
                    Console.WriteLine($"Workflow {workflowId} not found in project {projectId}");
                    return NotFound($"Workflow with ID {workflowId} not found in project {projectId}");
                }

                if (workflow.WorkflowType != WorkflowType.CodeFirst)
                {
                    return BadRequest("Setting PR URL is only supported for CodeFirst workflows");
                }

                Console.WriteLine($"Before: PullRequestUrl='{workflow.PullRequestUrl}', PRCreatedAt={workflow.PRCreatedAt}");

                var codeFirstWorkflow = new CodeFirstWorkflow(workflow);
                Console.WriteLine($"Current state machine state: {codeFirstWorkflow.CurrentState}");

                // Validate that we're in the correct state to set PR URL
                if (workflow.PRCreatedAt.HasValue && !string.IsNullOrEmpty(workflow.PullRequestUrl))
                {
                    Console.WriteLine("PR URL already set, but allowing update...");
                }

                if (!workflow.CodeWorkStartedAt.HasValue)
                {
                    return BadRequest("Code work must be completed before setting PR URL");
                }

                // Set the PR URL in the context FIRST
                workflow.PullRequestUrl = request.PullRequestUrl;
                workflow.PRCreatedAt = DateTime.UtcNow;

                Console.WriteLine($"Set workflow properties: PullRequestUrl='{workflow.PullRequestUrl}', PRCreatedAt={workflow.PRCreatedAt}");

                // Then trigger the state machine
                await codeFirstWorkflow.SetPullRequestAsync(request.PullRequestUrl);

                Console.WriteLine($"After state machine: Current state = {codeFirstWorkflow.CurrentState}");
                Console.WriteLine($"Final workflow state: PullRequestUrl='{workflow.PullRequestUrl}', PRCreatedAt={workflow.PRCreatedAt}");

                // Persist the changes
                _projectService.UpdateWorkflow(workflow);
                Console.WriteLine("Changes persisted to ProjectService");

                // Verify the saved data
                var savedWorkflow = _projectService.GetWorkflowById(workflowId);
                Console.WriteLine($"Verified saved: PullRequestUrl='{savedWorkflow?.PullRequestUrl}', PRCreatedAt={savedWorkflow?.PRCreatedAt}");

                // Return the updated workflow state
                return GetWorkflowById(workflowId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting PR URL: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
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

            Console.WriteLine("Returning: Created (default)");
            return "Created";
        }


        private string DeriveCodeFirstState(ConfigCleanupContext workflow)
        {
            Console.WriteLine($"=== DeriveCodeFirstState Debug ===");
            Console.WriteLine($"IsCompleted: {workflow.IsCompleted}");
            Console.WriteLine($"ErrorMessage: '{workflow.ErrorMessage}'");
            Console.WriteLine($"CodeWorkStartedAt: {workflow.CodeWorkStartedAt}");
            Console.WriteLine($"PRCreatedAt: {workflow.PRCreatedAt}");
            Console.WriteLine($"PRApprovedAt: {workflow.PRApprovedAt}");
            Console.WriteLine($"PRMergedAt: {workflow.PRMergedAt}");
            Console.WriteLine($"DeploymentDetectedAt: {workflow.DeploymentDetectedAt}");
            Console.WriteLine($"PullRequestUrl: '{workflow.PullRequestUrl}'");

            // For CodeFirst workflows, determine state based on progress timestamps
            if (!workflow.CodeWorkStartedAt.HasValue)
            {
                Console.WriteLine("Returning: Created (code work not started)");
                return "Created";
            }

            if (!workflow.PRCreatedAt.HasValue && string.IsNullOrEmpty(workflow.PullRequestUrl))
            {
                Console.WriteLine("Returning: AwaitingUserAction (need to create PR)");
                return "AwaitingUserAction";
            }

            if (!workflow.PRApprovedAt.HasValue)
            {
                Console.WriteLine("Returning: AwaitingUserAction (need to mark PR as approved)");
                return "AwaitingUserAction"; // This will show the Proceed button
            }

            if (!workflow.PRMergedAt.HasValue)
            {
                Console.WriteLine("Returning: AwaitingUserAction (need to confirm merge)");
                return "AwaitingUserAction";
            }

            if (!workflow.DeploymentDetectedAt.HasValue)
            {
                Console.WriteLine("Returning: AwaitingUserAction (need to confirm deployment)");
                return "AwaitingUserAction";
            }

            Console.WriteLine("Returning: Completed (all steps done)");
            Console.WriteLine("=== End DeriveCodeFirstState Debug ===");
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
            Console.WriteLine($"ArchiveConfiguration is null: {workflow.ArchiveConfiguration == null}");

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
            return result;
        }

        private string DeriveTransformToDefaultState(ConfigCleanupContext workflow)
        {
            Console.WriteLine("=== DeriveTransformToDefaultState Debug ===");
            Console.WriteLine($"IsCompleted: {workflow.IsCompleted}");
            Console.WriteLine($"ErrorMessage: '{workflow.ErrorMessage}'");
            Console.WriteLine($"HasError: {!string.IsNullOrEmpty(workflow.ErrorMessage)}");
            Console.WriteLine($"TransformStartedAt: {workflow.TransformStartedAt}");
            Console.WriteLine($"HasBeenStarted: {workflow.TransformStartedAt.HasValue}");

            // For TransformToDefault workflows:
            // - If not started (TransformStartedAt is null), show as "Created" -> UI shows "Start" button
            // - If started but not completed, show as "AwaitingUserAction" -> UI shows "Proceed" button

            if (!workflow.IsCompleted && string.IsNullOrEmpty(workflow.ErrorMessage))
            {
                if (workflow.TransformStartedAt.HasValue)
                {
                    Console.WriteLine("Returning: AwaitingUserAction (started, ready to proceed)");
                    Console.WriteLine("=== End DeriveTransformToDefaultState Debug ===");
                    return "AwaitingUserAction";
                }
                else
                {
                    Console.WriteLine("Returning: Created (not started yet)");
                    Console.WriteLine("=== End DeriveTransformToDefaultState Debug ===");
                    return "Created";
                }
            }

            Console.WriteLine("Returning: Created (default for completed/error case)");
            Console.WriteLine("=== End DeriveTransformToDefaultState Debug ===");
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

        private string DetermineInitialStateForDebug(ConfigCleanupContext context)
        {
            Console.WriteLine($"=== Debug DetermineInitialState ===");
            Console.WriteLine($"IsCompleted: {context.IsCompleted}");
            Console.WriteLine($"ErrorMessage: '{context.ErrorMessage}'");
            Console.WriteLine($"HasError: {!string.IsNullOrEmpty(context.ErrorMessage)}");
            Console.WriteLine($"TransformStartedAt: {context.TransformStartedAt}");
            Console.WriteLine($"HasBeenStarted: {context.TransformStartedAt.HasValue}");

            if (context.IsCompleted)
            {
                Console.WriteLine("Would return: Completed");
                return "Completed";
            }

            if (!string.IsNullOrEmpty(context.ErrorMessage))
            {
                Console.WriteLine("Would return: Failed");
                return "Failed";
            }

            // Check if workflow has been started
            if (context.TransformStartedAt.HasValue)
            {
                Console.WriteLine("Would return: AwaitingUserAction (already started)");
                return "AwaitingUserAction";
            }

            Console.WriteLine("Would return: Created");
            return "Created";
        }

        private string GetCodeFirstCurrentAction(ConfigCleanupContext workflow)
        {
            Console.WriteLine($"=== GetCodeFirstCurrentAction Debug ===");
            Console.WriteLine($"CodeWorkStartedAt.HasValue: {workflow.CodeWorkStartedAt.HasValue}");
            Console.WriteLine($"PRCreatedAt.HasValue: {workflow.PRCreatedAt.HasValue}");
            Console.WriteLine($"PRApprovedAt.HasValue: {workflow.PRApprovedAt.HasValue}");
            Console.WriteLine($"PRMergedAt.HasValue: {workflow.PRMergedAt.HasValue}");
            Console.WriteLine($"DeploymentDetectedAt.HasValue: {workflow.DeploymentDetectedAt.HasValue}");

            if (!workflow.CodeWorkStartedAt.HasValue)
            {
                Console.WriteLine("Returning: StartCodeWork");
                return "StartCodeWork";
            }

            if (!workflow.PRCreatedAt.HasValue)
            {
                Console.WriteLine("Returning: CreatePR");
                return "CreatePR"; // This will need the separate endpoint
            }

            if (!workflow.PRApprovedAt.HasValue)
            {
                Console.WriteLine("Returning: MarkPRApproved");
                return "MarkPRApproved";
            }

            if (!workflow.PRMergedAt.HasValue)
            {
                Console.WriteLine("Returning: ConfirmMerge");
                return "ConfirmMerge";
            }

            if (!workflow.DeploymentDetectedAt.HasValue)
            {
                Console.WriteLine("Returning: ConfirmDeployment");
                return "ConfirmDeployment";
            }

            Console.WriteLine("Returning: Complete");
            Console.WriteLine("=== End GetCodeFirstCurrentAction Debug ===");
            return "Complete";
        }
    }
}
