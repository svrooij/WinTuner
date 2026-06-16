using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.DeviceManagement.Monitoring.AlertRecords.MicrosoftGraphDeviceManagementGetPortalNotifications;
using Microsoft.Graph.Beta.Models;
using WingetIntune.Extensions;
using WingetIntune.Intune;

namespace WingetIntune.Graph;

public static class GraphWorkflows
{
    public static async Task AddIntuneCategoriesToAppAsync(this GraphServiceClient graphServiceClient, string appId, string[] categories, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(graphServiceClient);
        ArgumentException.ThrowIfNullOrEmpty(appId);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(cancellationToken);
#endif
        // Load categories to match against
        var graphCategories = await graphServiceClient.DeviceAppManagement.MobileAppCategories.GetAsync(cancellationToken: cancellationToken);

        // Match categories by name and add to app
        var foundCategories = categories.Select(c => graphCategories!.Value!.SingleOrDefault(x => x.DisplayName?.Equals(c, StringComparison.InvariantCultureIgnoreCase) == true)?.Id).Where(c => !string.IsNullOrEmpty(c)).ToArray();
        var batch = new Microsoft.Graph.BatchRequestContentCollection(graphServiceClient);
        foreach (var categoryId in foundCategories)
        {
            await batch.AddBatchRequestStepAsync(graphServiceClient.Intune_AddCategoryToApp_RequestInfo(appId, categoryId!));
        }
        await graphServiceClient.Batch.PostAsync(batch, cancellationToken);
    }

    public static async Task<int> AssignAppAsync(this GraphServiceClient graphServiceClient, string appId, string[]? requiredFor, string[]? availableFor, string[]? uninstallFor, bool addAutoUpdateSetting, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(graphServiceClient);
        ArgumentException.ThrowIfNullOrEmpty(appId);
        ArgumentNullException.ThrowIfNull(cancellationToken);
#endif
        List<MobileAppAssignment> assignments = new List<MobileAppAssignment>();
        if (requiredFor is not null && requiredFor.Any())
        {
            assignments.AddRange(GenerateAssignments(requiredFor, InstallIntent.Required));
        }

        if (availableFor is not null && availableFor.Any())
        {
            assignments.AddRange(GenerateAssignments(availableFor, InstallIntent.Available, addAutoUpdateSetting));
        }

        if (uninstallFor is not null && uninstallFor.Any())
        {
            assignments.AddRange(GenerateAssignments(uninstallFor, InstallIntent.Uninstall));
        }

        if (assignments.Count == 0)
        {
            return -1;
        }

        await graphServiceClient.DeviceAppManagement.MobileApps[appId].Assign.PostAsync(new Microsoft.Graph.Beta.DeviceAppManagement.MobileApps.Item.Assign.AssignPostRequestBody
        {
            MobileAppAssignments = assignments
        }, cancellationToken: cancellationToken);
        return assignments.Count;
    }

    public static async Task<bool> EnableAppAutoUpdateOnExistingAssignmentsAsync(this GraphServiceClient graphServiceClient, string appId, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(graphServiceClient);
        ArgumentException.ThrowIfNullOrEmpty(appId);
        ArgumentNullException.ThrowIfNull(cancellationToken);
#endif

        var app = await graphServiceClient.DeviceAppManagement.MobileApps[appId].GetAsync(req =>
        {
            req.QueryParameters.Expand = new[] { "assignments" };
        }, cancellationToken: cancellationToken);

        if (app is null || app.Assignments is null || !app.Assignments.Any())
        {
            return false; // No assignments found
        }

        var newAssignments = app.Assignments;
        foreach (var assignment in newAssignments)
        {
            if (assignment.Intent == InstallIntent.Available)
            {
                assignment.Settings ??= new Win32LobAppAssignmentSettings();
                if (assignment.Settings is Win32LobAppAssignmentSettings win32Settings)
                {
                    win32Settings.Notifications = Win32LobAppNotification.ShowReboot;
                    win32Settings.AutoUpdateSettings ??= new Win32LobAppAutoUpdateSettings();
                    win32Settings.AutoUpdateSettings.AutoUpdateSupersededAppsState = Win32LobAutoUpdateSupersededAppsState.Enabled;
                }
            }
        }

        await graphServiceClient.DeviceAppManagement.MobileApps[appId].Assign.PostAsync(new Microsoft.Graph.Beta.DeviceAppManagement.MobileApps.Item.Assign.AssignPostRequestBody
        {
            MobileAppAssignments = newAssignments
        }, cancellationToken: cancellationToken);
        return true;
    }

    private static List<MobileAppAssignment> GenerateAssignments(string[] groups, InstallIntent intent, bool addSetting = false)
    {
        List<MobileAppAssignment> assignments = new List<MobileAppAssignment>();
        MobileAppAssignmentSettings? settings = null;
        if (intent == InstallIntent.Available && addSetting)
        {
            settings = new Win32LobAppAssignmentSettings
            {
                Notifications = Win32LobAppNotification.ShowReboot,
                AutoUpdateSettings = new Win32LobAppAutoUpdateSettings { AutoUpdateSupersededAppsState = Win32LobAutoUpdateSupersededAppsState.Enabled }
            };
        }
        if (groups is not null && groups.Any())
        {
            var groupsGuids = groups.Where(x => Guid.TryParse(x, out _));
            if (groupsGuids.Count() > 0)
            {
                assignments.AddRange(groupsGuids.Select(x => CreateAssignment(intent, new GroupAssignmentTarget { GroupId = x }, settings)));
            }
            if (groups.ContainsIgnoreCase(IntuneManagerConstants.AllUsers))
            {
                assignments.Add(CreateAssignment(intent, new AllLicensedUsersAssignmentTarget(), settings));
            }
            if (groups.ContainsIgnoreCase(IntuneManagerConstants.AllDevices))
            {
                assignments.Add(CreateAssignment(intent, new AllDevicesAssignmentTarget(), settings));
            }
        }
        return assignments;
    }

    private static MobileAppAssignment CreateAssignment(InstallIntent intent, DeviceAndAppManagementAssignmentTarget target, MobileAppAssignmentSettings? settings = null)
    {
        var a = new MobileAppAssignment
        {
            Intent = intent,
            Target = target,
            Settings = settings
        };
        return a;
    }
}
