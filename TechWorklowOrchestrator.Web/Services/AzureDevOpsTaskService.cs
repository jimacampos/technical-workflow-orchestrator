using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using TechWorklowOrchestrator.Web.Models;

namespace TechWorklowOrchestrator.Web.Services
{
    public class AzureDevOpsTaskService : IAzureDevOpsTaskService, IDisposable
    {
        private VssConnection? _connection;
        private WorkItemTrackingHttpClient? _workItemClient;
        private readonly ILogger<AzureDevOpsTaskService> _logger;

        public AzureDevOpsTaskService(ILogger<AzureDevOpsTaskService> logger)
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

                Disconnect();

                var credentials = new VssBasicCredential(string.Empty, personalAccessToken);
                _connection = new VssConnection(new Uri(organizationUrl), credentials);
                _workItemClient = _connection.GetClient<WorkItemTrackingHttpClient>();

                // Test connection by getting a simple query
                var wiql = new Wiql { Query = "SELECT [System.Id] FROM WorkItems WHERE [System.WorkItemType] = 'Task' ORDER BY [System.CreatedDate] DESC" };
                var result = await _workItemClient.QueryByWiqlAsync(wiql, top: 1);

                _logger.LogInformation("Successfully connected to Azure DevOps Work Item Tracking.");

                return new AdoConnectionResult
                {
                    IsSuccess = true,
                    Message = "Successfully connected to Work Item Tracking!"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Azure DevOps Work Item Tracking");
                return new AdoConnectionResult
                {
                    IsSuccess = false,
                    Message = $"Connection failed: {ex.Message}",
                    Exception = ex
                };
            }
        }

        public async Task<WorkItem?> GetWorkItemAsync(int workItemId)
        {
            if (_workItemClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                return await _workItemClient.GetWorkItemAsync(workItemId, expand: WorkItemExpand.All);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get work item {WorkItemId}", workItemId);
                throw;
            }
        }

        public async Task<List<WorkItem>> GetWorkItemsAsync(string projectName, string wiql)
        {
            if (_workItemClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                var wiqlQuery = new Wiql { Query = wiql };
                var result = await _workItemClient.QueryByWiqlAsync(wiqlQuery, projectName);

                if (result.WorkItems?.Any() != true)
                    return new List<WorkItem>();

                var ids = result.WorkItems.Select(wi => wi.Id).ToArray();
                return await _workItemClient.GetWorkItemsAsync(ids, expand: WorkItemExpand.All);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get work items with query: {Query}", wiql);
                throw;
            }
        }

        public async Task<List<WorkItem>> GetMyWorkItemsAsync(string projectName)
        {
            var wiql = $@"
                SELECT [System.Id], [System.Title], [System.State], [System.WorkItemType], [System.AssignedTo], [System.CreatedDate]
                FROM WorkItems 
                WHERE [System.TeamProject] = '{projectName}' 
                AND [System.AssignedTo] = @Me
                AND [System.State] <> 'Closed' 
                AND [System.State] <> 'Done'
                ORDER BY [System.CreatedDate] DESC";

            return await GetWorkItemsAsync(projectName, wiql);
        }

        public async Task<List<WorkItem>> GetRecentWorkItemsAsync(string projectName, int count = 20)
        {
            var wiql = $@"
                SELECT [System.Id], [System.Title], [System.State], [System.WorkItemType], [System.AssignedTo], [System.CreatedDate]
                FROM WorkItems 
                WHERE [System.TeamProject] = '{projectName}'
                ORDER BY [System.CreatedDate] DESC";

            return await GetWorkItemsAsync(projectName, wiql);
        }

        public async Task<WorkItem> CreateWorkItemAsync(string projectName, string workItemType, Dictionary<string, object> fields)
        {
            if (_workItemClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                var patchDocument = new JsonPatchDocument();

                foreach (var field in fields)
                {
                    patchDocument.Add(new JsonPatchOperation()
                    {
                        Operation = Operation.Add,
                        Path = $"/fields/{field.Key}",
                        Value = field.Value
                    });
                }

                return await _workItemClient.CreateWorkItemAsync(patchDocument, projectName, workItemType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create work item of type {WorkItemType} in project {Project}", workItemType, projectName);
                throw;
            }
        }

        public async Task<WorkItem> UpdateWorkItemAsync(int workItemId, Dictionary<string, object> fields)
        {
            if (_workItemClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                var patchDocument = new JsonPatchDocument();

                foreach (var field in fields)
                {
                    patchDocument.Add(new JsonPatchOperation()
                    {
                        Operation = Operation.Replace,
                        Path = $"/fields/{field.Key}",
                        Value = field.Value
                    });
                }

                return await _workItemClient.UpdateWorkItemAsync(patchDocument, workItemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update work item {WorkItemId}", workItemId);
                throw;
            }
        }

        public async Task<List<WorkItem>> GetWorkItemsByStateAsync(string projectName, string state)
        {
            var wiql = $@"
                SELECT [System.Id], [System.Title], [System.State], [System.WorkItemType], [System.AssignedTo], [System.CreatedDate]
                FROM WorkItems 
                WHERE [System.TeamProject] = '{projectName}' 
                AND [System.State] = '{state}'
                ORDER BY [System.CreatedDate] DESC";

            return await GetWorkItemsAsync(projectName, wiql);
        }

        public async Task<List<WorkItem>> GetWorkItemsByAssigneeAsync(string projectName, string assignee)
        {
            var wiql = $@"
                SELECT [System.Id], [System.Title], [System.State], [System.WorkItemType], [System.AssignedTo], [System.CreatedDate]
                FROM WorkItems 
                WHERE [System.TeamProject] = '{projectName}' 
                AND [System.AssignedTo] = '{assignee}'
                ORDER BY [System.CreatedDate] DESC";

            return await GetWorkItemsAsync(projectName, wiql);
        }

        public async Task<List<WorkItem>> GetWorkItemsByIterationAsync(string projectName, string iterationPath)
        {
            var wiql = $@"
                SELECT [System.Id], [System.Title], [System.State], [System.WorkItemType], [System.AssignedTo], [System.IterationPath]
                FROM WorkItems 
                WHERE [System.TeamProject] = '{projectName}' 
                AND [System.IterationPath] UNDER '{iterationPath}'
                ORDER BY [System.CreatedDate] DESC";

            return await GetWorkItemsAsync(projectName, wiql);
        }

        public async Task<List<WorkItemType>> GetWorkItemTypesAsync(string projectName)
        {
            if (_workItemClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                return await _workItemClient.GetWorkItemTypesAsync(projectName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get work item types for project {Project}", projectName);
                throw;
            }
        }

        public async Task<List<WorkItemStateColor>> GetWorkItemStatesAsync(string projectName, string workItemType)
        {
            if (_workItemClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                return await _workItemClient.GetWorkItemTypeStatesAsync(projectName, workItemType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get work item states for type {WorkItemType} in project {Project}", workItemType, projectName);
                throw;
            }
        }

        public async Task<List<WorkItemComment>> GetWorkItemCommentsAsync(int workItemId)
        {
            if (_workItemClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                var result = await _workItemClient.GetCommentsAsync(workItemId);
                return result.Comments.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get comments for work item {WorkItemId}", workItemId);
                throw;
            }
        }

        public async Task<WorkItemComment> AddWorkItemCommentAsync(string projectName, int workItemId, string comment)
        {
            if (_workItemClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");
            try
            {
                var commentRequest = new CommentCreate { Text = comment };
                var result = await _workItemClient.AddCommentAsync(commentRequest, projectName, workItemId);
                return new WorkItemComment
                {
                    Text = result.Text,
                    RevisedBy = new IdentityReference
                    {
                        Id = Guid.TryParse(result.ModifiedBy.Id, out var guid) ? guid : Guid.Empty,
                        DisplayName = result.ModifiedBy.DisplayName,
                        UniqueName = result.ModifiedBy.UniqueName
                    },
                    RevisedDate = result.ModifiedDate
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add comment to work item {WorkItemId}", workItemId);
                throw;
            }
        }

        public async Task<List<WorkItemUpdate>> GetWorkItemUpdatesAsync(int workItemId)
        {
            if (_workItemClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                return await _workItemClient.GetUpdatesAsync(workItemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get updates for work item {WorkItemId}", workItemId);
                throw;
            }
        }

        public async Task<List<AttachmentReference>> GetWorkItemAttachmentsAsync(int workItemId)
        {
            if (_workItemClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                var workItem = await _workItemClient.GetWorkItemAsync(workItemId, expand: WorkItemExpand.Relations);
                var attachments = new List<AttachmentReference>();

                if (workItem.Relations != null)
                {
                    foreach (var relation in workItem.Relations.Where(r => r.Rel == "AttachedFile"))
                    {
                        if (relation.Attributes?.ContainsKey("name") == true)
                        {
                            attachments.Add(new AttachmentReference
                            {
                                Id = Guid.Parse(relation.Url.Split('/').Last()),
                                Url = relation.Url
                            });
                        }
                    }
                }

                return attachments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get attachments for work item {WorkItemId}", workItemId);
                throw;
            }
        }

        public async Task<AttachmentReference> AddWorkItemAttachmentAsync(int workItemId, string fileName, byte[] content)
        {
            if (_workItemClient == null)
                throw new InvalidOperationException("Not connected to Azure DevOps. Call TestConnectionAsync first.");

            try
            {
                // Create a temporary file with the content
                var tempPath = Path.GetTempFileName();
                var actualFileName = Path.ChangeExtension(tempPath, Path.GetExtension(fileName));
                File.Move(tempPath, actualFileName);

                try
                {
                    // Write the bytes to the file
                    await File.WriteAllBytesAsync(actualFileName, content);

                    // Upload the file as an attachment
                    var attachmentReference = await _workItemClient.CreateAttachmentAsync(
                        actualFileName,
                        uploadType: "Simple");

                    return attachmentReference;
                }
                finally
                {
                    // Clean up temp file
                    if (File.Exists(actualFileName))
                        File.Delete(actualFileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add attachment to work item {WorkItemId}", workItemId);
                throw;
            }
        }

        public void Disconnect()
        {
            _workItemClient?.Dispose();
            _connection?.Dispose();
            _workItemClient = null;
            _connection = null;
            _logger.LogInformation("Disconnected from Azure DevOps Work Item Tracking");
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
