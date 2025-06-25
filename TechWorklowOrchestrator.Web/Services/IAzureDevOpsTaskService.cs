using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using TechWorklowOrchestrator.Web.Models;

namespace TechWorklowOrchestrator.Web.Services
{
    public interface IAzureDevOpsTaskService
    {
        Task<AdoConnectionResult> TestConnectionAsync(string organizationUrl, string personalAccessToken);

        // Work Item Management
        Task<WorkItem?> GetWorkItemAsync(int workItemId);
        Task<List<WorkItem>> GetWorkItemsAsync(string projectName, string wiql);
        Task<List<WorkItem>> GetMyWorkItemsAsync(string projectName);
        Task<List<WorkItem>> GetRecentWorkItemsAsync(string projectName, int count = 20);
        Task<WorkItem> CreateWorkItemAsync(string projectName, string workItemType, Dictionary<string, object> fields);
        Task<WorkItem> UpdateWorkItemAsync(int workItemId, Dictionary<string, object> fields);

        // Work Item Queries
        Task<List<WorkItem>> GetWorkItemsByStateAsync(string projectName, string state);
        Task<List<WorkItem>> GetWorkItemsByAssigneeAsync(string projectName, string assignee);
        Task<List<WorkItem>> GetWorkItemsByIterationAsync(string projectName, string iterationPath);

        // Work Item Types and Metadata
        Task<List<WorkItemType>> GetWorkItemTypesAsync(string projectName);
        Task<List<WorkItemStateColor>> GetWorkItemStatesAsync(string projectName, string workItemType);

        // Comments and History
        Task<List<WorkItemComment>> GetWorkItemCommentsAsync(int workItemId);
        Task<WorkItemComment> AddWorkItemCommentAsync(string projectName, int workItemId, string comment);
        Task<List<WorkItemUpdate>> GetWorkItemUpdatesAsync(int workItemId);

        // Attachments
        Task<List<AttachmentReference>> GetWorkItemAttachmentsAsync(int workItemId);
        Task<AttachmentReference> AddWorkItemAttachmentAsync(int workItemId, string fileName, byte[] content);

        void Disconnect();
    }
}
