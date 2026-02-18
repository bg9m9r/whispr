using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Whispr.Client.Models;

namespace Whispr.Client.Views;

internal static class ViewHelpers
{
    public static ServerTreeNode? FindNodeAtVisual(object? visual)
    {
        if (visual is null) return null;
        for (var v = visual as Visual; v != null; v = v.GetVisualParent())
        {
            if (v is TreeViewItem tvi && tvi.DataContext is ServerTreeNode node)
                return node;
        }
        return null;
    }
}
