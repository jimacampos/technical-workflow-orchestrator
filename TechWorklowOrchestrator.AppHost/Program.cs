var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.TechWorklowOrchestrator_ApiService>("apiservice");

builder.AddProject<Projects.TechWorklowOrchestrator_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
