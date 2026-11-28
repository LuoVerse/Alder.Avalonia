namespace Alder.Avalonia.Controls;

internal sealed class AlderFileTreeScrollViewer() : AlderScrollViewer(resolveVerticalThumbEnd: true)
{
    protected override Type StyleKeyOverride => typeof(AlderScrollViewer);

    protected override void ScrollToVerticalThumbEnd()
    {
        if (TemplatedParent is not AlderFileTree tree)
        {
            base.ScrollToVerticalThumbEnd();
            return;
        }

        // Realizing an expanded branch may change the root estimate on the following layout pass.
        for (int attempt = 0; attempt < 4; attempt++)
        {
            var last = tree.RealizeLastVisibleRow();
            base.ScrollToVerticalThumbEnd();
            UpdateLayout();
            if (last is { IsEffectivelyVisible: true } && Math.Abs(Offset.Y - ScrollBarMaximum.Y) < 0.5)
                break;
        }
    }
}
