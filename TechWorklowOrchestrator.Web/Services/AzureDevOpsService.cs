using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using TechWorklowOrchestrator.Web.Models;

namespace TechWorklowOrchestrator.Web.Services
{
    public class AzureDevOpsService : IAzureDevOpsService, IDisposable
    {
        private VssConnection? _connection;
        private ProjectHttpClient? _projectClient;
        private GitHttpClient? _gitClient;
        private BuildHttpClient? _buildClient;
        private readonly ILogger<AzureDevOpsService> _logger;

        public AzureDevOpsService(ILogger<AzureDevOpsService> logger)
        {
            _logger = logger;
        }

        public async Task<AdoConnectionResult> TestConnectionAsync(string organizationUrl, string personalAccessToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(organizationUrl) || string.IsNullOrWhiteSpace(personalAccessToken))
                {
                    return new AdoConnectionResult
                    {
                        IsSuccess = false,
                        Message = "Organization URL and Personal Access Token are required"
                    };
                }

                // Disconnect any existing connection
                Disconnect();

                var credentials = new VssBasicCredential(string.Empty, personalAccessToken);
                _connection = new VssConnection(new Uri(organizationUrl), credentials);

                // Initialize clients
                _projectClient = _connection.GetClient<ProjectHttpClient>();
                _gitClient = _connection.GetClient<GitHttpClient>();
                _buildClient = _connection.GetClient<BuildHttpClient>();

                // Test connection by getting projects
                var projects = await _projectClient.GetProjects();

                _logger.LogInformation("Successfully connected to Azure DevOps. Found {ProjectCount} projects.", projects.Count);

                return new AdoConnectionResult
                {
                    IsSuccess = true,
                    Message = $"Successfully connected! Found {projects.Count} projects.",
                    Projects = projects
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Azure DevOps");

                return new AdoConnectionResult
                {
                    IsSuccess = false,
                    Message = $"Connection failed: {ex.Message}",
                    Exception = ex
                };
            }
        }

        public async Task<IPagedList<TeamProjectReference>> GetProjectsAsync()
        {
            if (_projectClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                return await _projectClient.GetProjects();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get projects");
                throw;
            }
        }

        public async Task<GitPullRequest?> GetPullRequestAsync(string projectName, string repositoryName, int pullRequestId)
        {
            if (_gitClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                return await _gitClient.GetPullRequestAsync(projectName, repositoryName, pullRequestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get pull request {PullRequestId} from {Repository}", pullRequestId, repositoryName);
                throw;
            }
        }

        public async Task<List<GitPullRequest>> GetRecentPullRequestsAsync(string projectName, string repositoryName, int count = 20)
        {
            if (_gitClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                var searchCriteria = new GitPullRequestSearchCriteria
                {
                    Status = PullRequestStatus.All
                };

                return await _gitClient.GetPullRequestsAsync(
                    projectName,
                    repositoryName,
                    searchCriteria,
                    top: count
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get recent pull requests from {Repository}", repositoryName);
                throw;
            }
        }

        public async Task<List<Build>> GetBuildsAsync(string projectName, int? buildDefinitionId = null, int count = 10)
        {
            if (_buildClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                return await _buildClient.GetBuildsAsync(
                    projectName,
                    definitions: buildDefinitionId.HasValue ? new[] { buildDefinitionId.Value } : null,
                    top: count
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get builds for project {Project}", projectName);
                throw;
            }
        }

        public async Task<List<BuildDefinitionReference>> GetBuildDefinitionsAsync(string projectName)
        {
            if (_buildClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                return await _buildClient.GetDefinitionsAsync(projectName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get build definitions for project {Project}", projectName);
                throw;
            }
        }

        public async Task<List<GitRepository>> GetRepositoriesAsync(string projectName)
        {
            if (_gitClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                return await _gitClient.GetRepositoriesAsync(projectName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get repositories for project {Project}", projectName);
                throw;
            }
        }

        public void Disconnect()
        {
            _projectClient?.Dispose();
            _gitClient?.Dispose();
            _buildClient?.Dispose();
            _connection?.Dispose();

            _projectClient = null;
            _gitClient = null;
            _buildClient = null;
            _connection = null;

            _logger.LogInformation("Disconnected from Azure DevOps");
        }

        public async Task<Build?> GetBuildAsync(string projectName, int buildId)
        {
            if (_buildClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                return await _buildClient.GetBuildAsync(projectName, buildId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get build {BuildId} for project {Project}", buildId, projectName);
                throw;
            }
        }

        public async Task<Timeline?> GetBuildTimelineAsync(string projectName, int buildId)
        {
            if (_buildClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                return await _buildClient.GetBuildTimelineAsync(projectName, buildId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get build timeline for build {BuildId} in project {Project}", buildId, projectName);
                throw;
            }
        }

        public async Task<List<BuildLog>> GetBuildLogsAsync(string projectName, int buildId)
        {
            if (_buildClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                return await _buildClient.GetBuildLogsAsync(projectName, buildId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get build logs for build {BuildId} in project {Project}", buildId, projectName);
                throw;
            }
        }

        public async Task<string> GetBuildLogContentAsync(string projectName, int buildId, int logId)
        {
            if (_buildClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                using var logStream = await _buildClient.GetBuildLogAsync(projectName, buildId, logId);
                using var reader = new StreamReader(logStream);
                return await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get build log content for build {BuildId}, log {LogId} in project {Project}", buildId, logId, projectName);
                throw;
            }
        }

        public async Task<List<BuildArtifact>> GetBuildArtifactsAsync(string projectName, int buildId)
        {
            if (_buildClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                return await _buildClient.GetArtifactsAsync(projectName, buildId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get build artifacts for build {BuildId} in project {Project}", buildId, projectName);
                throw;
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
