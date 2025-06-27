using TechWorklowOrchestrator.ApiService.Dto;
using TechWorklowOrchestrator.ApiService.Repository;
using TechWorklowOrchestrator.ApiService.Service;
using TechWorklowOrchestrator.Core;
using TechWorklowOrchestrator.Core.Workflow;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Add services
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
}); 
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IGenericWorkflowRepository, InMemoryGenericWorkflowRepository>();
builder.Services.AddSingleton(typeof(IGenericWorkflowService<,>), typeof(GenericWorkflowService<,>));

// Register the generic project repository and service for ConfigCleanupContext
builder.Services.AddSingleton<IGenericProjectRepository<ConfigCleanupContext>, InMemoryGenericProjectRepository<ConfigCleanupContext>>();
builder.Services.AddSingleton<IGenericProjectService<ConfigCleanupContext>, GenericProjectService<ConfigCleanupContext>>();

builder.Services.AddSingleton<IGenericProjectRepository<CodeUpdateContext>, InMemoryGenericProjectRepository<CodeUpdateContext>>();
builder.Services.AddSingleton<IGenericProjectService<CodeUpdateContext>, GenericProjectService<CodeUpdateContext>>();

builder.Services.AddSingleton<IWorkflowProvider<CreateWorkflowRequest, ConfigCleanupContext>, ConfigCleanupWorkflowProvider>();
builder.Services.AddSingleton<IWorkflowProvider<CodeUpdateWorkflowRequest, CodeUpdateContext>, CodeUpdateWorkflowProvider>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

app.MapDefaultEndpoints();
app.MapControllers();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
