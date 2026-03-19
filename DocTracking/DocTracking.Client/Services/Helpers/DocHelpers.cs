using MudBlazor;

namespace DocTracking.Client.Services.Helpers
{
    public class DocHelpers
    {
        public static string GetIconForAction(string? action) => action switch
        {
            "Created" => Icons.Material.Filled.NoteAdd,
            "Forwarded" => Icons.Material.Filled.DirectionsWalk,
            "Received" => Icons.Material.Filled.Inbox,
            "Completed" => Icons.Material.Filled.CheckCircle,
            _ => Icons.Material.Filled.Circle
        };

        public static Color GetActionColor(string? action) => action switch
        {
            "Created" => Color.Info,
            "Forwarded" => Color.Warning,
            "Received" => Color.Primary,
            "Completed" => Color.Success,
            _ => Color.Default
        };

        public static Color GetTimelineColor(string? action, bool isCurrent) => action switch
        {
            "Created" when isCurrent => Color.Info,
            "Forwarded" when isCurrent => Color.Warning,
            "Received" when isCurrent => Color.Primary,
            "Completed" when isCurrent => Color.Success,
            _ => Color.Default
        };

        public static Color GetLocationColor(string? action) => action switch
        {
            "Created" or "Forwarded" => Color.Warning,
            _ => Color.Default
        };

        public static string GetLocationIcon(string? action) => action switch
        {
            "Created" or "Forwarded" => Icons.Material.Filled.Send,
            _ => Icons.Material.Filled.LocationOn,
        };

        public static string GetLocationLabel(string? action) =>
            action is "Created" or "Forwarded" ? "Sent to" : "Location";

        public static Color GetPriorityColor(string? action) => action switch
        {
            "Emergency" => Color.Error,
            "Urgent" => Color.Warning,
            "Medium" => Color.Info,
            "Low" => Color.Success,
            _ => Color.Default
        };

        public static Color GetStatusColor(string? action) => action switch
        {
            "Completed" => Color.Success,
            "Received" => Color.Info,
            "In Motion" => Color.Warning,
            "Archived" => Color.Dark,
            _ => Color.Default
        };

    }
}
