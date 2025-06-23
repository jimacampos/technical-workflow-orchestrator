using Microsoft.Azure.Cosmos;
using System.Net;
using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.ApiService.Repository
{
    public class CosmosDbWorkflowRepository : IWorkflowRepository
    {
        private readonly Container _container;
        private const string PartitionKeyPath = "/workflowType";

        public CosmosDbWorkflowRepository(CosmosClient cosmosClient, string databaseName, string containerName)
        {
            _container = cosmosClient.GetContainer(databaseName, containerName);
        }

        public async Task<Guid> CreateAsync(WorkflowInstance workflow)
        {
            workflow.Id = Guid.NewGuid();
            workflow.LastUpdated = DateTime.UtcNow;

            var cosmosWorkflow = ToCosmosDocument(workflow);

            await _container.CreateItemAsync(
                cosmosWorkflow,
                new PartitionKey(workflow.WorkflowType.ToString())
            );

            return workflow.Id;
        }

        public async Task<WorkflowInstance> GetByIdAsync(Guid id)
        {
            try
            {
                // Since we don't know the partition key, we'll need to query across partitions
                var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                    .WithParameter("@id", id.ToString());

                using var resultSet = _container.GetItemQueryIterator<CosmosWorkflowDocument>(query);

                if (resultSet.HasMoreResults)
                {
                    var response = await resultSet.ReadNextAsync();
                    var document = response.FirstOrDefault();
                    return document != null ? FromCosmosDocument(document) : null;
                }

                return null;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<IEnumerable<WorkflowInstance>> GetAllAsync()
        {
            var query = new QueryDefinition("SELECT * FROM c");
            var workflows = new List<WorkflowInstance>();

            using var resultSet = _container.GetItemQueryIterator<CosmosWorkflowDocument>(query);

            while (resultSet.HasMoreResults)
            {
                var response = await resultSet.ReadNextAsync();
                workflows.AddRange(response.Select(FromCosmosDocument));
            }

            return workflows;
        }

        public async Task<IEnumerable<WorkflowInstance>> GetByStateAsync(WorkflowState state)
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.currentState = @state")
                .WithParameter("@state", state.ToString());

            var workflows = new List<WorkflowInstance>();

            using var resultSet = _container.GetItemQueryIterator<CosmosWorkflowDocument>(query);

            while (resultSet.HasMoreResults)
            {
                var response = await resultSet.ReadNextAsync();
                workflows.AddRange(response.Select(FromCosmosDocument));
            }

            return workflows;
        }

        public async Task<IEnumerable<WorkflowInstance>> GetByTypeAsync(WorkflowType type)
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.workflowType = @type")
                .WithParameter("@type", type.ToString());

            var workflows = new List<WorkflowInstance>();

            using var resultSet = _container.GetItemQueryIterator<CosmosWorkflowDocument>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(type.ToString())
                }
            );

            while (resultSet.HasMoreResults)
            {
                var response = await resultSet.ReadNextAsync();
                workflows.AddRange(response.Select(FromCosmosDocument));
            }

            return workflows;
        }

        public async Task UpdateAsync(WorkflowInstance workflow)
        {
            workflow.LastUpdated = DateTime.UtcNow;
            var cosmosWorkflow = ToCosmosDocument(workflow);

            await _container.ReplaceItemAsync(
                cosmosWorkflow,
                workflow.Id.ToString(),
                new PartitionKey(workflow.WorkflowType.ToString())
            );
        }

        public async Task DeleteAsync(Guid id)
        {
            try
            {
                // First, we need to find the item to get its partition key
                var workflow = await GetByIdAsync(id);
                if (workflow != null)
                {
                    await _container.DeleteItemAsync<CosmosWorkflowDocument>(
                        id.ToString(),
                        new PartitionKey(workflow.WorkflowType.ToString())
                    );
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Item already deleted or doesn't exist
            }
        }

        private static CosmosWorkflowDocument ToCosmosDocument(WorkflowInstance workflow)
        {
            return new CosmosWorkflowDocument
            {
                Id = workflow.Id.ToString(),
                ConfigurationName = workflow.ConfigurationName,
                WorkflowType = workflow.WorkflowType.ToString(),
                CurrentState = workflow.CurrentState.ToString(),
                Context = workflow.Context,
                CreatedAt = workflow.CreatedAt,
                LastUpdated = workflow.LastUpdated,
                ErrorMessage = workflow.ErrorMessage,
                Metadata = workflow.Metadata,
                History = workflow.History
            };
        }

        private static WorkflowInstance FromCosmosDocument(CosmosWorkflowDocument document)
        {
            return new WorkflowInstance
            {
                Id = Guid.Parse(document.Id),
                ConfigurationName = document.ConfigurationName,
                WorkflowType = Enum.Parse<WorkflowType>(document.WorkflowType),
                CurrentState = Enum.Parse<WorkflowState>(document.CurrentState),
                Context = document.Context,
                CreatedAt = document.CreatedAt,
                LastUpdated = document.LastUpdated,
                ErrorMessage = document.ErrorMessage,
                Metadata = document.Metadata ?? new Dictionary<string, string>(),
                History = document.History ?? new List<WorkflowEvent>()
            };
        }
    }

    // Cosmos DB document model
    public class CosmosWorkflowDocument
    {
        public string Id { get; set; }
        public string ConfigurationName { get; set; }
        public string WorkflowType { get; set; }
        public string CurrentState { get; set; }
        public ConfigCleanupContext Context { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUpdated { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
        public List<WorkflowEvent> History { get; set; }
    }
}