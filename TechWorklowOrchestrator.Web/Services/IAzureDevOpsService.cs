using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using TechWorklowOrchestrator.Web.Models;

namespace TechWorklowOrchestrator.Web.Services
{
    public interface IAzureDevOpsService
    {
        Task<AdoConnectionResult> TestConnectionAsync(string organizationUrl, string personalAccessToken);
        Task<IPagedList<TeamProjectReference>> GetProjectsAsync();
        Task<GitPullRequest?> GetPullRequestAsync(string projectName, string repositoryName, int pullRequestId);
        Task<List<GitPullRequest>> GetRecentPullRequestsAsync(string projectName, string repositoryName, int count = 20);
        Task<List<Build>> GetBuildsAsync(string projectName, int? buildDefinitionId = null, int count = 10);
        Task<List<BuildDefinitionReference>> GetBuildDefinitionsAsync(string projectName);
        Task<List<GitRepository>> GetRepositoriesAsync(string projectName);
        void Disconnect();

        Task<Build?> GetBuildAsync(string projectName, int buildId);
        Task<Timeline?> GetBuildTimelineAsync(string projectName, int buildId);
        Task<List<BuildLog>> GetBuildLogsAsync(string projectName, int buildId);
        Task<string> GetBuildLogContentAsync(string projectName, int buildId, int logId);
        Task<List<BuildArtifact>> GetBuildArtifactsAsync(string projectName, int buildId);
    }
}
