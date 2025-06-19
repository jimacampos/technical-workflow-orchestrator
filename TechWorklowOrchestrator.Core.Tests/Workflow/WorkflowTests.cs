using FluentAssertions;
using TechWorklowOrchestrator.Core.Workflow;

namespace TechWorklowOrchestrator.Core.Tests.Workflow
{
    [TestFixture]
    public class WorkflowTests
    {
        [Test]
        public async Task ArchiveOnlyWorkflow_Should_Complete_Successfully()
        {
            var context = new ConfigCleanupContext
            {
                ConfigurationName = "legacy-feature-flag",
                WorkflowType = WorkflowType.ArchiveOnly,
                CurrentTrafficPercentage = 100
            };

            var workflow = WorkflowFactory.CreateWorkflow(context);

            (await workflow.GetCurrentStatusAsync()).Should().Be("Ready to start traffic reduction");
            await workflow.StartAsync();
            await Task.Delay(250); // Allow async state transitions
            (await workflow.GetCurrentStatusAsync()).Should().Be("✅ Configuration archived successfully");
        }

        [Test]
        public async Task CodeFirstWorkflow_Should_Progress_Through_States()
        {
            var context = new ConfigCleanupContext
            {
                ConfigurationName = "deprecated-api-config",
                WorkflowType = WorkflowType.CodeFirst
            };

            var workflow = (CodeFirstWorkflow)WorkflowFactory.CreateWorkflow(context);

            (await workflow.GetCurrentStatusAsync()).Should().Be("Ready to create PR");
            await workflow.StartAsync();
            await Task.Delay(350); // Wait for PR creation

            (await workflow.GetCurrentStatusAsync()).Should().Be($"⏳ Awaiting PR review: {context.PullRequestUrl}");
            await workflow.NotifyPRApprovedAsync();
            await Task.Delay(150); // Wait for PR merge

            (await workflow.GetCurrentStatusAsync()).Should().Be("⏳ Waiting for deployment");
            await workflow.NotifyDeploymentCompletedAsync();
            await Task.Delay(200); // Wait for archiving

            (await workflow.GetCurrentStatusAsync()).Should().Be("✅ Code removed and configuration archived");
        }

        [Test]
        public async Task TransformToDefaultWorkflow_Should_Complete_Successfully()
        {
            var context = new ConfigCleanupContext
            {
                ConfigurationName = "simple-setting",
                WorkflowType = WorkflowType.TransformToDefault
            };

            var workflow = WorkflowFactory.CreateWorkflow(context);

            (await workflow.GetCurrentStatusAsync()).Should().Be("Ready to transform");
            await workflow.StartAsync();
            await Task.Delay(250); // Allow async state transitions
            (await workflow.GetCurrentStatusAsync()).Should().Be("✅ Configuration transformed to defaults");
        }
    }
}
