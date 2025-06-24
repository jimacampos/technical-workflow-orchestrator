using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace TechWorklowOrchestrator.Web.Models
{
    public class AdoConnectionResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = "";
        public IPagedList<TeamProjectReference> Projects { get; set; }
        public Exception? Exception { get; set; }
    }
}
