using Avalonia;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

internal sealed class AlderMorphingTabHeadersPanel : Panel
{
    private const double CompactWidth = 88;
    private const double PreferredMinimumWidth = 96;
    private const double MaximumWidth = 176;
    private const double PreferredGap = 16;

    protected override Size MeasureOverride(Size availableSize)
    {
        var slot = new Size(MaximumWidth, availableSize.Height);
        double width = 0;
        double height = 0;
        foreach (var tab in Children)
        {
            if (!tab.IsVisible) continue;
            tab.Measure(slot);
            width += CompactWidth;
            height = Math.Max(height, tab.DesiredSize.Height);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int count = 0;
        foreach (var tab in Children)
            if (tab.IsVisible) count++;
        if (count == 0) return finalSize;

        double gap = count > 1
            ? Math.Clamp((finalSize.Width - count * PreferredMinimumWidth) / (count - 1), 0, PreferredGap)
            : 0;
        double width = Math.Clamp((finalSize.Width - gap * (count - 1)) / count, CompactWidth, MaximumWidth);
        var slot = new Size(width, finalSize.Height);
        double x = 0;
        foreach (var tab in Children)
        {
            if (!tab.IsVisible) continue;
            tab.Arrange(new Rect(new Point(x, 0), slot));
            x += width + gap;
        }
        return finalSize;
    }
}
