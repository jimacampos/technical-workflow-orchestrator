using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TechWorklowOrchestrator.ApiService.Dto;
using TechWorklowOrchestrator.ApiService.Service;
using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Controllers
{
    [ApiController]
    [Route("api/codeupdate-workflows")]
    public class CodeUpdateWorkflowsController : ControllerBase
    {
        private readonly IGenericWorkflowService<CodeUpdateWorkflowRequest, CodeUpdateContext> _workflowService;

        public CodeUpdateWorkflowsController(
            IGenericWorkflowService<CodeUpdateWorkflowRequest, CodeUpdateContext> workflowService)
        {
            _workflowService = workflowService;
        }

        // Only POST and DELETE remain here

        // POST: api/codeupdate-workflows
        [HttpPost]
        public async Task<ActionResult<CodeUpdateContext>> CreateWorkflow([FromBody] CodeUpdateWorkflowRequest request)
        {
            var id = await _workflowService.CreateWorkflowAsync(request);
            var workflow = await _workflowService.GetWorkflowAsync(id);
            return Ok(workflow); // No redirect to AllWorkflowsController
        }

        // DELETE: api/codeupdate-workflows/{id}
        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> DeleteWorkflow(Guid id)
        {
            var deleted = await _workflowService.DeleteWorkflowAsync(id);
            if (!deleted)
                return NotFound();
            return NoContent();
        }
    }
}