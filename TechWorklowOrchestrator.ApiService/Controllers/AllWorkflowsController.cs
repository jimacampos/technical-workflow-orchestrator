using Microsoft.AspNetCore.Mvc;
using TechWorklowOrchestrator.ApiService.Dto;
using TechWorklowOrchestrator.ApiService.Repository;
using TechWorklowOrchestrator.ApiService.Service;
using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Controllers
{
    [ApiController]
    [Route("api/all-workflows")]
    public class AllWorkflowsController : ControllerBase
    {
        private readonly IGenericWorkflowService<CreateWorkflowRequest, ConfigCleanupContext> _configCleanupWorkflowService;
        private readonly IGenericWorkflowService<CodeUpdateWorkflowRequest, CodeUpdateContext> _codeUpdateWorkflowService;
        private readonly IGenericWorkflowRepository _repository;

        public AllWorkflowsController(
            IGenericWorkflowService<CreateWorkflowRequest, ConfigCleanupContext> configCleanupWorkflowService,
            IGenericWorkflowService<CodeUpdateWorkflowRequest, CodeUpdateContext> codeUpdateWorkflowService,
            IGenericWorkflowRepository repository)
        {
            _configCleanupWorkflowService = configCleanupWorkflowService;
            _codeUpdateWorkflowService = codeUpdateWorkflowService;
            _repository = repository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorkflowResponse>>> GetAllWorkflows()
        {
            var instances = await _repository.GetAllAsync();
            var configIds = instances.Where(i => i.WorkflowType != "CodeUpdate").Select(i => i.Id).ToList();
            var codeUpdateIds = instances.Where(i => i.WorkflowType == "CodeUpdate").Select(i => i.Id).ToList();

            var configCleanupWorkflows = new List<WorkflowResponse>();
            var codeUpdateWorkflows = new List<WorkflowResponse>();

            foreach (var id in configIds)
            {
                var workflow = await _configCleanupWorkflowService.GetWorkflowAsync(id);
                if (workflow != null)
                    configCleanupWorkflows.Add(workflow);
            }

            foreach (var id in codeUpdateIds)
            {
                var workflow = await _codeUpdateWorkflowService.GetWorkflowAsync(id);
                if (workflow != null)
                    codeUpdateWorkflows.Add(workflow);
            }

            var allWorkflows = configCleanupWorkflows.Concat(codeUpdateWorkflows).ToList();
            return Ok(allWorkflows);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<WorkflowResponse>> GetWorkflow(Guid id)
        {
            var instance = await _repository.GetByIdAsync(id);
            if (instance == null)
                return NotFound();

            if (instance.WorkflowType != "CodeUpdate")
            {
                var configWorkflow = await _configCleanupWorkflowService.GetWorkflowAsync(id);
                if (configWorkflow != null)
                    return Ok(configWorkflow);
            }
            else if (instance.WorkflowType == "CodeUpdate")
            {
                var codeUpdateWorkflow = await _codeUpdateWorkflowService.GetWorkflowAsync(id);
                if (codeUpdateWorkflow != null)
                    return Ok(codeUpdateWorkflow);
            }

            return NotFound();
        }

        [HttpGet("summary")]
        public async Task<ActionResult<object>> GetWorkflowsSummary()
        {
            // Only call one service, since each returns a summary for all workflows
            var summary = await _configCleanupWorkflowService.GetSummaryAsync();

            // Optionally, if you want to use the code update service instead, that's fine too:
            // var summary = await _codeUpdateWorkflowService.GetSummaryAsync();

            return Ok(summary);
        }
    }
}