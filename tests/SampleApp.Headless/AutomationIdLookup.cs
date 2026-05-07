using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace SampleApp.Headless;

internal static class AutomationIdLookup
{
    public static Control? FindByAutomationId(Control root, string id)
    {
        foreach (var d in root.GetSelfAndLogicalDescendants())
        {
            if (d is Control c && AutomationProperties.GetAutomationId(c) == id)
                return c;
        }
        return null;
    }

    public static IEnumerable<(Control Control, string Id)> EnumerateAllIds(Control root)
    {
        foreach (var d in root.GetSelfAndLogicalDescendants())
        {
            if (d is Control c)
            {
                var id = AutomationProperties.GetAutomationId(c);
                if (!string.IsNullOrEmpty(id)) yield return (c, id);
            }
        }
    }
}
