using Microsoft.AspNetCore.Mvc;
using TechWorklowOrchestrator.ApiService.Dto;
using TechWorklowOrchestrator.ApiService.Service;
using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;
using TechWorklowOrchestrator.Web.Models;
using CreateWorkflowRequest = TechWorklowOrchestrator.ApiService.Dto.CreateWorkflowRequest;
using ExternalEventRequest = TechWorklowOrchestrator.ApiService.Dto.ExternalEventRequest;
using WorkflowResponse = TechWorklowOrchestrator.ApiService.Dto.WorkflowResponse;

namespace TechWorklowOrchestrator.ApiService.Controllers
{
    [ApiController]
    [Route("api/config-workflows")]
    public class WorkflowsController : ControllerBase
    {
        private readonly IGenericWorkflowService<CreateWorkflowRequest, ConfigCleanupContext> _workflowService;

        public WorkflowsController(IGenericWorkflowService<CreateWorkflowRequest, ConfigCleanupContext> workflowService)
        {
            _workflowService = workflowService;
        }

        // Only POST, DELETE, and workflow actions remain

        /// <summary>
        /// Create a new workflow
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<WorkflowResponse>> CreateWorkflow(CreateWorkflowRequest request)
        {
            var id = await _workflowService.CreateWorkflowAsync(request);
            var workflow = await _workflowService.GetWorkflowAsync(id);
            return Ok(workflow); // No redirect to AllWorkflowsController
        }

        /// <summary>
        /// Start a workflow
        /// </summary>
        [HttpPost("{id:guid}/start")]
        public async Task<ActionResult<WorkflowResponse>> StartWorkflow(Guid id)
        {
            try
            {
                var workflow = await _workflowService.StartWorkflowAsync(id);
                return Ok(workflow);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Handle external event for a workflow
        /// </summary>
        [HttpPost("{id:guid}/events")]
        public async Task<ActionResult<WorkflowResponse>> HandleExternalEvent(Guid id, ExternalEventRequest eventRequest)
        {
            try
            {
                var workflow = await _workflowService.HandleExternalEventAsync(id, eventRequest);
                return Ok(workflow);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Delete a workflow
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> DeleteWorkflow(Guid id)
        {
            var deleted = await _workflowService.DeleteWorkflowAsync(id);
            if (!deleted)
                return NotFound($"Workflow {id} not found");

            return NoContent();
        }

        /// <summary>
        /// Proceed with manual action for a workflow
        /// </summary>
        [HttpPost("{id:guid}/proceed")]
        public async Task<ActionResult<WorkflowResponse>> ProceedWorkflow(Guid id)
        {
            try
            {
                var workflow = await _workflowService.GetWorkflowAsync(id);
                if (workflow == null)
                    return NotFound($"Workflow {id} not found");

                var updatedWorkflow = await _workflowService.ProceedWorkflowAsync(id);
                return Ok(updatedWorkflow);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
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
    }
}
