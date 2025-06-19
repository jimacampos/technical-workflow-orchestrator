using Microsoft.AspNetCore.Mvc;
using TechWorklowOrchestrator.ApiService.Dto;
using TechWorklowOrchestrator.ApiService.Service;

namespace TechWorklowOrchestrator.ApiService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkflowsController : ControllerBase
    {
        private readonly IWorkflowService _workflowService;

        public WorkflowsController(IWorkflowService workflowService)
        {
            _workflowService = workflowService;
        }

        /// <summary>
        /// Get summary of all workflows
        /// </summary>
        [HttpGet("summary")]
        public async Task<ActionResult<WorkflowSummary>> GetSummary()
        {
            var summary = await _workflowService.GetSummaryAsync();
            return Ok(summary);
        }

        /// <summary>
        /// Get all workflows
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorkflowResponse>>> GetAllWorkflows()
        {
            var workflows = await _workflowService.GetAllWorkflowsAsync();
            return Ok(workflows);
        }

        /// <summary>
        /// Get a specific workflow by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<WorkflowResponse>> GetWorkflow(Guid id)
        {
            var workflow = await _workflowService.GetWorkflowAsync(id);
            if (workflow == null)
                return NotFound($"Workflow {id} not found");

            return Ok(workflow);
        }

        /// <summary>
        /// Create a new workflow
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<WorkflowResponse>> CreateWorkflow(CreateWorkflowRequest request)
        {
            var id = await _workflowService.CreateWorkflowAsync(request);
            var workflow = await _workflowService.GetWorkflowAsync(id);

            return CreatedAtAction(nameof(GetWorkflow), new { id }, workflow);
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
    }
}
