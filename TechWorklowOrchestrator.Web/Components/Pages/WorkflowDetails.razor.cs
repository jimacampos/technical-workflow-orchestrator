using System.Timers;
using Microsoft.AspNetCore.Components;
using TechWorklowOrchestrator.Web.Models;
using TechWorklowOrchestrator.Web.Services;

namespace TechWorklowOrchestrator.Web.Components.Pages
{
    public partial class WorkflowDetails : IDisposable
    {
        [Parameter] public Guid Id { get; set; }

        private WorkflowResponse? workflow;
        private bool isLoading = true;
        private bool showEventModal = false;
        private string selectedEventType = "";
        private string eventDataJson = "{}";
        private bool isProcessingEvent = false;
        private List<StageProgressModel>? stageProgress;

        // New properties for improved UX
        private bool isStartingWorkflow = false;
        private System.Timers.Timer? autoRefreshTimer;
        private bool isAutoRefreshing = false;
        private DateTime lastRefresh = DateTime.MinValue;
        private bool isProceedingWorkflow = false;
        private bool showPRUrlModal = false;
        private string prUrlInput = "";
        private bool isSettingPRUrl = false;

        [Inject] private IWorkflowApiService WorkflowApi { get; set; } = default!;
        [Inject] private NavigationManager Navigation { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            await LoadWorkflow();
            StartAutoRefresh();
        }

        protected override async Task OnParametersSetAsync()
        {
            if (workflow?.Id != Id)
            {
                StopAutoRefresh();
                await LoadWorkflow();
                StartAutoRefresh();
            }
        }

        public void Dispose()
        {
            StopAutoRefresh();
        }

        private void StartAutoRefresh()
        {
            if (ShouldAutoRefresh())
            {
                autoRefreshTimer = new System.Timers.Timer(5000); // 5 seconds
                autoRefreshTimer.Elapsed += async (sender, e) =>
                {
                    await InvokeAsync(async () =>
                    {
                        await RefreshWorkflow();
                        StateHasChanged();
                    });
                };
                autoRefreshTimer.AutoReset = true;
                autoRefreshTimer.Start();
            }
        }

        private void StopAutoRefresh()
        {
            autoRefreshTimer?.Stop();
            autoRefreshTimer?.Dispose();
            autoRefreshTimer = null;
        }

        private bool ShouldAutoRefresh()
        {
            if (workflow == null) return false;

            if (workflow.WorkflowType == WorkflowType.ArchiveOnly &&
                (workflow.CurrentState == WorkflowState.InProgress ||
                 workflow.CurrentState == WorkflowState.Waiting ||
                 workflow.CurrentState == WorkflowState.AwaitingUserAction ||
                 workflow.CurrentState == WorkflowState.Created))
            {
                return true;
            }

            if (workflow.WorkflowType == WorkflowType.CodeFirst &&
                (workflow.CurrentState == WorkflowState.InProgress ||
                 workflow.CurrentState == WorkflowState.AwaitingReview ||
                 workflow.CurrentState == WorkflowState.WaitingForDeployment ||
                 workflow.CurrentState == WorkflowState.AwaitingUserAction ||
                 workflow.CurrentState == WorkflowState.Created))
            {
                return true;
            }

            return false;
        }

        private async Task AutoRefresh()
        {
            if (isAutoRefreshing || isLoading) return;

            try
            {
                isAutoRefreshing = true;
                await LoadWorkflow(silent: true);

                if (!ShouldAutoRefresh())
                {
                    StopAutoRefresh();
                }

                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Auto-refresh failed: {ex.Message}");
            }
            finally
            {
                isAutoRefreshing = false;
            }
        }

        private async Task LoadWorkflow(bool silent = false)
        {
            if (!silent)
            {
                isLoading = true;
            }

            try
            {
                workflow = await WorkflowApi.GetWorkflowAsync(Id);
                lastRefresh = DateTime.Now;

                if (workflow?.WorkflowType == WorkflowType.ArchiveOnly &&
                    workflow.Metadata.ContainsKey("stageProgress"))
                {
                    try
                    {
                        var stageProgressJson = workflow.Metadata["stageProgress"];
                        stageProgress = System.Text.Json.JsonSerializer.Deserialize<List<StageProgressModel>>(stageProgressJson);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing stage progress: {ex.Message}");
                        stageProgress = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading workflow: {ex.Message}");
            }
            finally
            {
                if (!silent)
                {
                    isLoading = false;
                }
            }
        }

        private async Task RefreshWorkflow()
        {
            await LoadWorkflow();
        }

        private async Task StartWorkflow()
        {
            isStartingWorkflow = true;
            try
            {
                workflow = await WorkflowApi.StartWorkflowAsync(Id);

                StopAutoRefresh();
                StartAutoRefresh();

                await LoadWorkflow(silent: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting workflow: {ex.Message}");
            }
            finally
            {
                isStartingWorkflow = false;
            }
        }

        private async Task HandleExternalEvent()
        {
            if (string.IsNullOrEmpty(selectedEventType) || workflow == null)
                return;

            isProcessingEvent = true;
            try
            {
                var eventData = new Dictionary<string, object>();
                if (!string.IsNullOrWhiteSpace(eventDataJson) && eventDataJson != "{}")
                {
                    eventData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(eventDataJson)
                               ?? new Dictionary<string, object>();
                }

                var eventRequest = new ExternalEventRequest
                {
                    EventType = selectedEventType,
                    Data = eventData
                };

                workflow = await WorkflowApi.HandleExternalEventAsync(Id, eventRequest);
                showEventModal = false;
                selectedEventType = "";
                eventDataJson = "{}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling external event: {ex.Message}");
            }
            finally
            {
                isProcessingEvent = false;
            }
        }

        private bool CanHandleExternalEvents(WorkflowResponse workflow)
        {
            return workflow.CurrentState switch
            {
                WorkflowState.WaitingAfter80Percent => true,
                _ => false
            };
        }

        private string GetStateBadgeClass(WorkflowState state) => state switch
        {
            WorkflowState.Created => "bg-secondary",
            WorkflowState.InProgress or WorkflowState.ReducingTo80Percent or WorkflowState.ReducingToZero or
            WorkflowState.Archiving or WorkflowState.CreatingPR or WorkflowState.Merged or WorkflowState.Transforming => "bg-primary",
            WorkflowState.Waiting or WorkflowState.WaitingAfter80Percent or WorkflowState.AwaitingReview or
            WorkflowState.WaitingForDeployment => "bg-warning",
            WorkflowState.AwaitingUserAction => "bg-info",
            WorkflowState.Completed => "bg-success",
            WorkflowState.Failed => "bg-danger",
            _ => "bg-secondary"
        };

        private string GetProgressBarClass(WorkflowState state) => state switch
        {
            WorkflowState.Completed => "bg-success",
            WorkflowState.Failed => "bg-danger",
            WorkflowState.Waiting or WorkflowState.WaitingAfter80Percent or WorkflowState.AwaitingReview or
            WorkflowState.WaitingForDeployment => "bg-warning",
            WorkflowState.AwaitingUserAction => "bg-info",
            _ => "bg-primary"
        };

        private string GetStageTimelineClass(string status) => status switch
        {
            "Completed" => "completed",
            "ReducingTraffic" or "Waiting" => "active",
            "Failed" => "failed",
            _ => ""
        };

        private string GetStageBadgeClass(string status) => status switch
        {
            "Completed" => "bg-success",
            "ReducingTraffic" => "bg-primary",
            "Waiting" => "bg-warning text-dark",
            "Failed" => "bg-danger",
            "Pending" => "bg-secondary",
            _ => "bg-light text-dark"
        };

        private string GetStageStatusText(string status) => status switch
        {
            "Pending" => "Pending",
            "ReducingTraffic" => "Reducing Traffic",
            "Waiting" => "Waiting",
            "Completed" => "Completed",
            "Failed" => "Failed",
            _ => status
        };

        private string GetStageProgressBarClass(string status) => status switch
        {
            "ReducingTraffic" => "bg-primary",
            "Waiting" => "bg-warning",
            _ => "bg-secondary"
        };

        private double GetStageProgressPercentage(StageProgressModel stage)
        {
            if (stage.Status == "ReducingTraffic")
            {
                var totalReduction = stage.TargetAllocationPercentage - stage.CurrentAllocationPercentage;
                if (totalReduction == 0) return 100;
                return 50;
            }
            else if (stage.Status == "Waiting")
            {
                if (stage.WaitStartTime.HasValue)
                {
                    var elapsed = DateTime.UtcNow - stage.WaitStartTime.Value;
                    var progress = (elapsed.TotalMilliseconds / stage.WaitDuration.TotalMilliseconds) * 100;
                    return Math.Min(progress, 100);
                }
            }
            return 0;
        }

        private string GetStageProgressText(StageProgressModel stage)
        {
            if (stage.Status == "ReducingTraffic")
            {
                return "Reducing traffic allocation...";
            }
            else if (stage.Status == "Waiting" && stage.WaitStartTime.HasValue)
            {
                var elapsed = DateTime.UtcNow - stage.WaitStartTime.Value;
                var remaining = stage.WaitDuration - elapsed;

                if (remaining.TotalSeconds > 0)
                {
                    return $"Waiting - {FormatDuration(remaining)} remaining";
                }
                else
                {
                    return "Wait period completed";
                }
            }
            return "";
        }

        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1)
            {
                return $"{(int)duration.TotalDays}d {duration.Hours}h";
            }
            else if (duration.TotalHours >= 1)
            {
                return $"{(int)duration.TotalHours}h {duration.Minutes}m";
            }
            else
            {
                return $"{(int)duration.TotalMinutes}m";
            }
        }

        private async Task ProceedWorkflow()
        {
            if (workflow == null) return;

            if (workflow.WorkflowType == WorkflowType.CodeFirst &&
                workflow.CurrentState == WorkflowState.AwaitingUserAction &&
                string.IsNullOrEmpty(workflow.PullRequestUrl))
            {
                showPRUrlModal = true;
                return;
            }

            isProceedingWorkflow = true;
            try
            {
                var response = await WorkflowApi.ProceedWorkflowAsync(workflow.ProjectId!.Value, workflow.Id);

                if (response != null)
                {
                    workflow = response;
                    await LoadWorkflow(silent: true);
                    StopAutoRefresh();
                    StartAutoRefresh();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error proceeding workflow: {ex.Message}");
            }
            finally
            {
                isProceedingWorkflow = false;
            }
        }

        private string GetProceedButtonText()
        {
            if (workflow?.CurrentState != WorkflowState.AwaitingUserAction)
                return "Proceed";

            if (workflow.WorkflowType == WorkflowType.CodeFirst)
            {
                return GetCodeFirstProceedButtonText();
            }

            var status = workflow.Status;
            if (status.Contains("Start Rolldown"))
                return "Start Rolldown";
            else if (status.Contains("Continue Rolldown"))
                return "Continue Rolldown";
            else if (status.Contains("Stop Rollout"))
                return "Stop Rollout";
            else if (status.Contains("Archive"))
                return "Archive";
            else
                return "Proceed";
        }

        private string GetCodeFirstProceedButtonText()
        {
            if (workflow == null) return "Proceed";

            if (!workflow.CodeWorkStartedAt.HasValue)
                return "Start Code Work";

            if (string.IsNullOrEmpty(workflow.PullRequestUrl))
                return "Set PR URL";

            if (!workflow.PRApprovedAt.HasValue)
                return "Mark PR as Approved";

            if (!workflow.PRMergedAt.HasValue)
                return "Confirm Merge";

            if (!workflow.DeploymentDetectedAt.HasValue)
                return "Confirm Deployment";

            return "Complete";
        }

        private async Task SetPullRequestUrl()
        {
            if (string.IsNullOrEmpty(prUrlInput) || workflow == null) return;

            isSettingPRUrl = true;
            try
            {
                var response = await WorkflowApi.SetPullRequestUrlAsync(workflow.ProjectId!.Value, workflow.Id, prUrlInput);

                if (response != null)
                {
                    workflow = response;
                    showPRUrlModal = false;
                    prUrlInput = "";
                    await LoadWorkflow(silent: true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting PR URL: {ex.Message}");
            }
            finally
            {
                isSettingPRUrl = false;
            }
        }

        private int GetCodeFirstStepProgress()
        {
            if (workflow?.WorkflowType != WorkflowType.CodeFirst) return 0;

            if (workflow.CurrentState == WorkflowState.Completed) return 5;

            if (workflow.Metadata.ContainsKey("deploymentDetectedAt") || workflow.DeploymentDetectedAt.HasValue) return 5;
            if (workflow.Metadata.ContainsKey("prMergedAt") || workflow.PRMergedAt.HasValue) return 4;
            if (workflow.Metadata.ContainsKey("prApprovedAt") || workflow.PRApprovedAt.HasValue) return 4;
            if (!string.IsNullOrEmpty(workflow.PullRequestUrl) ||
                workflow.Metadata.ContainsKey("prCreatedAt") ||
                workflow.PRCreatedAt.HasValue) return 3;
            if (workflow.Metadata.ContainsKey("codeWorkStartedAt") ||
                workflow.CodeWorkStartedAt.HasValue ||
                workflow.CurrentState != WorkflowState.Created) return 2;

            return 1;
        }

        private string GetCodeFirstProgressBarClass()
        {
            if (workflow?.CurrentState == WorkflowState.Completed) return "bg-success";
            if (workflow?.CurrentState == WorkflowState.Failed) return "bg-danger";
            return "bg-primary";
        }

        private string GetCodeFirstCurrentStepAlertClass()
        {
            return workflow?.CurrentState switch
            {
                WorkflowState.AwaitingUserAction => "alert-info",
                WorkflowState.AwaitingReview or WorkflowState.WaitingForDeployment => "alert-warning",
                WorkflowState.Completed => "alert-success",
                WorkflowState.Failed => "alert-danger",
                _ => "alert-light"
            };
        }

        private string GetCodeFirstCurrentStepTitle()
        {
            var step = GetCodeFirstStepProgress();
            return step switch
            {
                1 => "Step 1: Code Changes",
                2 => "Step 2: Create Pull Request",
                3 => "Step 3: PR Review & Approval",
                4 => "Step 4: Merge to Main",
                5 => "Step 5: Deployment",
                _ => "CodeFirst Workflow"
            };
        }

        private string GetCodeFirstCurrentStepDescription()
        {
            if (workflow?.CurrentState == WorkflowState.Completed)
                return "All steps completed! Configuration has been removed from code and deployed.";

            if (workflow?.CurrentState == WorkflowState.Failed)
                return $"Workflow failed: {workflow.ErrorMessage}";

            if (!workflow.CodeWorkStartedAt.HasValue)
                return "Click 'Start Code Work' to begin making code changes.";

            if (string.IsNullOrEmpty(workflow.PullRequestUrl))
                return "Code work completed. Click 'Set PR URL' to link your pull request.";

            if (!workflow.PRApprovedAt.HasValue)
                return "Pull request created. Click 'Mark PR as Approved' when the PR has been reviewed and approved.";

            if (!workflow.PRMergedAt.HasValue)
                return "PR approved. Click 'Confirm Merge' when the pull request has been merged to main.";

            if (!workflow.DeploymentDetectedAt.HasValue)
                return "Code merged. Click 'Confirm Deployment' when the deployment with your changes is complete.";

            return "Processing workflow step...";
        }

        public class StageProgressModel
        {
            public string Name { get; set; } = "";
            public string Status { get; set; } = "";
            public int CurrentAllocationPercentage { get; set; }
            public int TargetAllocationPercentage { get; set; }
            public DateTime? StartedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public DateTime? WaitStartTime { get; set; }
            public TimeSpan WaitDuration { get; set; }
            public string? ErrorMessage { get; set; }
        }
    }
}