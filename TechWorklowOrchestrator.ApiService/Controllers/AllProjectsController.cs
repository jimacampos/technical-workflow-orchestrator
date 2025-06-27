using Microsoft.AspNetCore.Mvc;
using TechWorklowOrchestrator.ApiService.Service;
using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;
using TechWorklowOrchestrator.Web.Models;
using ServiceName = TechWorklowOrchestrator.Core.ServiceName;
using WorkflowState = TechWorklowOrchestrator.Core.Workflow.WorkflowState;
using WorkflowType = TechWorklowOrchestrator.Core.Workflow.WorkflowType;

namespace TechWorklowOrchestrator.ApiService.Controllers
{
    [ApiController]
    [Route("api/all-projects")]
    public class AllProjectsController : ControllerBase
    {
        private readonly IGenericProjectService<ConfigCleanupContext> _configCleanupProjectService;
        private readonly IGenericProjectService<CodeUpdateContext> _codeUpdateProjectService;

        public AllProjectsController(
            IGenericProjectService<ConfigCleanupContext> configCleanupProjectService,
            IGenericProjectService<CodeUpdateContext> codeUpdateProjectService)
        {
            _configCleanupProjectService = configCleanupProjectService;
            _codeUpdateProjectService = codeUpdateProjectService;
        }

        [HttpGet]
        public ActionResult<IEnumerable<object>> GetAllProjects()
        {
            var configCleanupProjects = _configCleanupProjectService.GetAllProjects()
                .Select(p => new {
                    Id = p.Id,
                    Name = p.Name,
                    ServiceName = p.ServiceName,
                    Type = ProjectType.ConfigCleanup,
                    Description = p.Description,
                    CreatedAt = p.CreatedAt,
                    Status = p.Status,
                    Contexts = p.Contexts
                });

            var codeUpdateProjects = _codeUpdateProjectService.GetAllProjects()
                .Select(p => new {
                    Id = p.Id,
                    Name = p.Name,
                    ServiceName = p.ServiceName,
                    Type = ProjectType.CodeUpdate,
                    Description = p.Description,
                    CreatedAt = p.CreatedAt,
                    Status = p.Status,
                    Contexts = p.Contexts
                });

            var allProjects = configCleanupProjects.Concat<object>(codeUpdateProjects).ToList();
            return Ok(allProjects);
        }

        [HttpGet("service/{serviceName}")]
        public ActionResult<IEnumerable<object>> GetProjectsByService(ServiceName serviceName)
        {
            var configCleanupProjects = _configCleanupProjectService.GetProjectsByService(serviceName)
                .Select(p => new {
                    Id = p.Id,
                    Name = p.Name,
                    ServiceName = p.ServiceName,
                    Type = ProjectType.ConfigCleanup,
                    Description = p.Description,
                    CreatedAt = p.CreatedAt,
                    Status = p.Status,
                    Contexts = p.Contexts
                });

            var codeUpdateProjects = _codeUpdateProjectService.GetProjectsByService(serviceName)
                .Select(p => new {
                    Id = p.Id,
                    Name = p.Name,
                    ServiceName = p.ServiceName,
                    Type = ProjectType.CodeUpdate,
                    Description = p.Description,
                    CreatedAt = p.CreatedAt,
                    Status = p.Status,
                    Contexts = p.Contexts
                });

            var allProjects = configCleanupProjects.Concat<object>(codeUpdateProjects).ToList();
            return Ok(allProjects);
        }

        [HttpGet("{id}")]
        public ActionResult<object> GetProject(Guid id)
        {
            var configProject = _configCleanupProjectService.GetProjectById(id);
            if (configProject != null)
            {
                return Ok(new {
                    Id = configProject.Id,
                    Name = configProject.Name,
                    ServiceName = configProject.ServiceName,
                    Type = ProjectType.ConfigCleanup,
                    Description = configProject.Description,
                    CreatedAt = configProject.CreatedAt,
                    Status = configProject.Status,
                    Contexts = configProject.Contexts
                });
            }

            var codeUpdateProject = _codeUpdateProjectService.GetProjectById(id);
            if (codeUpdateProject != null)
            {
                return Ok(new {
                    Id = codeUpdateProject.Id,
                    Name = codeUpdateProject.Name,
                    ServiceName = codeUpdateProject.ServiceName,
                    Type = ProjectType.CodeUpdate,
                    Description = codeUpdateProject.Description,
                    CreatedAt = codeUpdateProject.CreatedAt,
                    Status = codeUpdateProject.Status,
                    Contexts = codeUpdateProject.Contexts
                });
            }

            return NotFound();
        }

        [HttpGet("{projectId}/workflows")]
        public ActionResult<IEnumerable<object>> GetWorkflowsByProject(Guid projectId)
        {
            var configCleanupWorkflows = _configCleanupProjectService.GetWorkflowsByProject(projectId)
                .Select(w => new {
                    Id = w.Id,
                    ConfigurationName = w.ConfigurationName,
                    WorkflowType = w.WorkflowType,
                    Type = ProjectType.ConfigCleanup,
                    Status = w is ConfigCleanupContext cc ? cc.GetCurrentStatusDescription() : null,
                    CreatedAt = w.CreatedAt,
                    ProjectId = w.ProjectId,
                    ErrorMessage = w.ErrorMessage
                });

            var codeUpdateWorkflows = _codeUpdateProjectService.GetWorkflowsByProject(projectId)
                .Select(w => new {
                    Id = w.Id,
                    ConfigurationName = w.Title,
                    WorkflowType = w.WorkflowType,
                    Type = ProjectType.CodeUpdate,
                    Status = w is CodeUpdateContext cu ? cu.GetCurrentStatusDescription() : null,
                    CreatedAt = w.CreatedAt,
                    ProjectId = w.ProjectId,
                    ErrorMessage = w.ErrorMessage
                });

            var allWorkflows = configCleanupWorkflows.Concat<object>(codeUpdateWorkflows).ToList();
            return Ok(allWorkflows);
        }

        [HttpGet("{projectId}/workflows/{workflowId}")]
        public ActionResult<object> GetWorkflow(Guid projectId, Guid workflowId)
        {
            // Try ConfigCleanup workflows
            var configWorkflow = _configCleanupProjectService.GetWorkflowById(workflowId);
            if (configWorkflow != null && configWorkflow.ProjectId == projectId)
            {
                return Ok(new
                {
                    Id = configWorkflow.Id,
                    ConfigurationName = configWorkflow.ConfigurationName,
                    WorkflowType = configWorkflow.WorkflowType,
                    Type = ProjectType.ConfigCleanup,
                    Status = configWorkflow.GetCurrentStatusDescription(),
                    CreatedAt = configWorkflow.CreatedAt,
                    ProjectId = configWorkflow.ProjectId,
                    ErrorMessage = configWorkflow.ErrorMessage
                });
            }

            // Try CodeUpdate workflows
            var codeUpdateWorkflow = _codeUpdateProjectService.GetWorkflowById(workflowId);
            if (codeUpdateWorkflow != null && codeUpdateWorkflow.ProjectId == projectId)
            {
                return Ok(new
                {
                    Id = codeUpdateWorkflow.Id,
                    ConfigurationName = codeUpdateWorkflow.Title,
                    WorkflowType = codeUpdateWorkflow.WorkflowType,
                    Type = ProjectType.CodeUpdate,
                    Status = codeUpdateWorkflow.GetCurrentStatusDescription(),
                    CreatedAt = codeUpdateWorkflow.CreatedAt,
                    ProjectId = codeUpdateWorkflow.ProjectId,
                    ErrorMessage = codeUpdateWorkflow.ErrorMessage
                });
            }

            return NotFound();
        }

        [HttpGet("workflows/{workflowId}")]
        public ActionResult<object> GetWorkflowById(Guid workflowId)
        {
            // Try ConfigCleanup workflows
            var configWorkflow = _configCleanupProjectService.GetWorkflowById(workflowId);
            if (configWorkflow != null)
            {
                // Build the metadata dictionary
                var metadata = new Dictionary<string, string>();

                // Add stage progress for ArchiveOnly workflows
                if (configWorkflow.WorkflowType == WorkflowType.ArchiveOnly && configWorkflow.ArchiveConfiguration != null)
                {
                    try
                    {
                        var stageProgressModels = configWorkflow.ArchiveConfiguration.Stages.Select(s => new
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
                        // Optionally log error
                    }
                }
                // Add CodeFirst metadata
                else if (configWorkflow.WorkflowType == WorkflowType.CodeFirst)
                {
                    try
                    {
                        if (configWorkflow.CodeWorkStartedAt.HasValue)
                            metadata["codeWorkStartedAt"] = configWorkflow.CodeWorkStartedAt.Value.ToString("O");
                        if (configWorkflow.PRCreatedAt.HasValue)
                            metadata["prCreatedAt"] = configWorkflow.PRCreatedAt.Value.ToString("O");
                        if (configWorkflow.PRApprovedAt.HasValue)
                            metadata["prApprovedAt"] = configWorkflow.PRApprovedAt.Value.ToString("O");
                        if (configWorkflow.PRMergedAt.HasValue)
                            metadata["prMergedAt"] = configWorkflow.PRMergedAt.Value.ToString("O");
                        if (configWorkflow.DeploymentDetectedAt.HasValue)
                            metadata["deploymentDetectedAt"] = configWorkflow.DeploymentDetectedAt.Value.ToString("O");
                    }
                    catch (Exception ex)
                    {
                        // Optionally log error
                    }
                }

                var response = new
                {
                    Id = configWorkflow.Id,
                    ConfigurationName = configWorkflow.ConfigurationName,
                    WorkflowType = configWorkflow.WorkflowType,
                    CurrentState = configWorkflow.IsCompleted
                        ? WorkflowState.Completed
                        : (!string.IsNullOrEmpty(configWorkflow.ErrorMessage)
                            ? WorkflowState.Failed
                            : WorkflowState.InProgress),
                    Status = configWorkflow.GetCurrentStatusDescription(),
                    CreatedAt = configWorkflow.CreatedAt,
                    LastUpdated = (DateTime?)null,
                    ErrorMessage = configWorkflow.ErrorMessage,
                    ProjectId = configWorkflow.ProjectId,
                    ProjectName = "", // Optionally resolve project name if needed
                    PullRequestUrl = configWorkflow.PullRequestUrl,
                    CodeWorkStartedAt = configWorkflow.CodeWorkStartedAt,
                    PRCreatedAt = configWorkflow.PRCreatedAt,
                    PRApprovedAt = configWorkflow.PRApprovedAt,
                    PRMergedAt = configWorkflow.PRMergedAt,
                    DeploymentDetectedAt = configWorkflow.DeploymentDetectedAt,
                    Progress = new
                    {
                        PercentComplete = configWorkflow.GetOverallProgress(),
                        CurrentStep = configWorkflow.ArchiveConfiguration?.CurrentStageIndex + 1 ?? 1,
                        TotalSteps = configWorkflow.ArchiveConfiguration?.Stages.Count ?? 1,
                        CurrentStepDescription = configWorkflow.GetCurrentStatusDescription(),
                        RequiresManualAction = false,
                        ManualActionDescription = ""
                    },
                    Metadata = metadata
                };

                return Ok(response);
            }

            // Try CodeUpdate workflows
            var codeUpdateWorkflow = _codeUpdateProjectService.GetWorkflowById(workflowId);
            if (codeUpdateWorkflow != null)
            {
                var response = new
                {
                    Id = codeUpdateWorkflow.Id,
                    ConfigurationName = codeUpdateWorkflow.Title,
                    WorkflowType = codeUpdateWorkflow.WorkflowType,
                    CurrentState = codeUpdateWorkflow.IsCompleted
                        ? WorkflowState.Completed
                        : (!string.IsNullOrEmpty(codeUpdateWorkflow.ErrorMessage)
                            ? WorkflowState.Failed
                            : WorkflowState.InProgress),
                    Status = codeUpdateWorkflow.GetStatusDescription(),
                    CreatedAt = codeUpdateWorkflow.CreatedAt,
                    LastUpdated = (DateTime?)null,
                    ErrorMessage = codeUpdateWorkflow.ErrorMessage,
                    ProjectId = codeUpdateWorkflow.ProjectId,
                    ProjectName = "", // Optionally resolve project name if needed
                    PullRequestUrl = codeUpdateWorkflow.PullRequestUrl,
                    Progress = new
                    {
                        PercentComplete = codeUpdateWorkflow.Progress,
                        CurrentStep = 1,
                        TotalSteps = 1,
                        CurrentStepDescription = codeUpdateWorkflow.GetStatusDescription(),
                        RequiresManualAction = false,
                        ManualActionDescription = ""
                    },
                    Metadata = codeUpdateWorkflow.Metadata ?? new Dictionary<string, string>()
                };

                return Ok(response);
            }

            return NotFound();
        }

        [HttpGet("workflows/summary")]
        public ActionResult<object> GetAllProjectWorkflowsSummary()
        {
            // Gather all workflows from both project types
            var configWorkflows = _configCleanupProjectService.GetAllWorkflows();
            var codeUpdateWorkflows = _codeUpdateProjectService.GetAllWorkflows();

            // Helper to derive state for ConfigCleanupContext (copied from ProjectsController)
            string DeriveCurrentState(ConfigCleanupContext workflow)
            {
                if (workflow.IsCompleted)
                    return "Completed";
                if (!string.IsNullOrEmpty(workflow.ErrorMessage))
                    return "Failed";
                if (workflow.WorkflowType == WorkflowType.ArchiveOnly)
                {
                    if (workflow.ArchiveConfiguration == null || !workflow.ArchiveConfiguration.WorkflowStartedAt.HasValue)
                        return "Created";
                    var currentStage = workflow.ArchiveConfiguration.CurrentStage;
                    if (currentStage == null)
                        return "Completed";
                    return currentStage.Status switch
                    {
                        WorkflowStageStatus.Pending => "AwaitingUserAction",
                        WorkflowStageStatus.ReducingTraffic => "InProgress",
                        WorkflowStageStatus.Waiting => "Waiting",
                        WorkflowStageStatus.Completed => "AwaitingUserAction",
                        WorkflowStageStatus.Failed => "Failed",
                        _ => "Created"
                    };
                }
                else if (workflow.WorkflowType == WorkflowType.TransformToDefault)
                {
                    if (!workflow.IsCompleted && string.IsNullOrEmpty(workflow.ErrorMessage))
                        return workflow.TransformStartedAt.HasValue ? "AwaitingUserAction" : "Created";
                    return "Created";
                }
                else if (workflow.WorkflowType == WorkflowType.CodeFirst)
                {
                    if (!workflow.CodeWorkStartedAt.HasValue)
                        return "Created";
                    if (!workflow.PRCreatedAt.HasValue && string.IsNullOrEmpty(workflow.PullRequestUrl))
                        return "AwaitingUserAction";
                    if (!workflow.PRApprovedAt.HasValue)
                        return "AwaitingUserAction";
                    if (!workflow.PRMergedAt.HasValue)
                        return "AwaitingUserAction";
                    if (!workflow.DeploymentDetectedAt.HasValue)
                        return "AwaitingUserAction";
                    return "Completed";
                }
                return "Created";
            }

            // Helper to derive state for CodeUpdateContext (minimal, adjust as needed)
            string DeriveCurrentStateForCodeUpdate(CodeUpdateContext workflow)
            {
                if (workflow.IsCompleted)
                    return "Completed";
                if (!string.IsNullOrEmpty(workflow.ErrorMessage))
                    return "Failed";
                // Add more state logic if needed for CodeUpdateContext
                return "InProgress";
            }

            // Combine all workflows into a single list of anonymous objects with a Type property
            var allWorkflows = configWorkflows
                .Select(w => new
                {
                    Type = "ConfigCleanup",
                    State = DeriveCurrentState(w),
                    IsCompleted = w.IsCompleted,
                    ErrorMessage = w.ErrorMessage,
                    WorkflowType = w.WorkflowType
                })
                .Concat(codeUpdateWorkflows.Select(w => new
                {
                    Type = "CodeUpdate",
                    State = DeriveCurrentStateForCodeUpdate(w), // Use the renamed function here
                    IsCompleted = w.IsCompleted,
                    ErrorMessage = w.ErrorMessage,
                    WorkflowType = w.WorkflowType
                }))
                .ToList();

            var summary = new
            {
                TotalWorkflows = allWorkflows.Count,
                ActiveWorkflows = allWorkflows.Count(w => w.State == "InProgress" || w.State == "Waiting" || w.State == "AwaitingUserAction"),
                CompletedWorkflows = allWorkflows.Count(w => w.IsCompleted),
                FailedWorkflows = allWorkflows.Count(w => !string.IsNullOrEmpty(w.ErrorMessage)),
                AwaitingManualAction = allWorkflows.Count(w => w.State == "AwaitingUserAction"),
                ByType = allWorkflows
                    .GroupBy(w => w.WorkflowType)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                ByState = allWorkflows
                    .GroupBy(w => w.State)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            return Ok(summary);
        }
    }
}