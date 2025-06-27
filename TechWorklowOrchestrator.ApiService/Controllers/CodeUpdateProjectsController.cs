using Microsoft.AspNetCore.Mvc;
using TechWorklowOrchestrator.ApiService.Dto;
using TechWorklowOrchestrator.ApiService.Service;
using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Controllers
{
    [ApiController]
    [Route("api/codeupdate-projects")]
    public class CodeUpdateProjectsController : ControllerBase
    {
        private readonly IGenericProjectService<CodeUpdateContext> _projectService;

        public CodeUpdateProjectsController(IGenericProjectService<CodeUpdateContext> projectService)
        {
            _projectService = projectService;
        }

        [HttpPost]
        public ActionResult<GenericProject<CodeUpdateContext>> CreateProject([FromBody] CreateProjectRequest request)
        {
            var project = _projectService.CreateProject(request.Name, request.ServiceName, request.Description);
            return Ok(project);
        }

        [HttpPost("{projectId}/workflows")]
        public ActionResult<CodeUpdateContext> CreateWorkflow(Guid projectId, [FromBody] CreateWorkflowRequest request)
        {
            try
            {
                // For CodeUpdateContext, set Title from Description or ConfigurationName if needed
                var workflow = _projectService.CreateWorkflow(projectId, request.ConfigurationName, request.WorkflowType);
                if (!string.IsNullOrWhiteSpace(request.Description))
                {
                    typeof(CodeUpdateContext).GetProperty("Title")?.SetValue(workflow, request.Description);
                }
                else if (!string.IsNullOrWhiteSpace(request.ConfigurationName))
                {
                    typeof(CodeUpdateContext).GetProperty("Title")?.SetValue(workflow, request.ConfigurationName);
                }
                _projectService.UpdateWorkflow(workflow);

                return Ok(workflow);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPut("{projectId}/workflows/{workflowId}")]
        public ActionResult<CodeUpdateContext> UpdateWorkflow(Guid projectId, Guid workflowId, [FromBody] CodeUpdateContext workflow)
        {
            var existingWorkflow = _projectService.GetWorkflowById(workflowId);
            if (existingWorkflow == null || existingWorkflow.ProjectId != projectId)
                return NotFound();

            workflow.Id = workflowId;
            workflow.ProjectId = projectId;
            _projectService.UpdateWorkflow(workflow);
            return Ok(workflow);
        }

    }
}