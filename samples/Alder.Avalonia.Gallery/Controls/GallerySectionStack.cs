using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Gallery.Controls;

/// <summary>Mounts sections one frame at a time on the browser, where views share the input thread.</summary>
internal sealed class GallerySectionStack : StackPanel
{
    private readonly Queue<Func<Control>> pendingSections = new();
    private bool scheduled;

    protected override Type StyleKeyOverride => typeof(StackPanel);

    public void AddSections(IEnumerable<Func<Control>> sections)
    {
        foreach (var build in sections)
        {
            if (OperatingSystem.IsBrowser())
                pendingSections.Enqueue(build);
            else
                Children.Add(build());
        }
        Schedule();
    }

    public bool LoadThrough(string sectionName)
    {
        int count = Children.Count;
        while (!Children.Any(section => section.Name == sectionName) && pendingSections.TryDequeue(out var build))
            Children.Add(build());
        return count != Children.Count;
    }

    internal void LoadRemainingSections()
    {
        while (pendingSections.TryDequeue(out var build))
            Children.Add(build());
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Schedule();
    }

    private void Schedule()
    {
        if (scheduled || pendingSections.Count == 0 || TopLevel.GetTopLevel(this) is not { } top) return;
        scheduled = true;
        top.RequestAnimationFrame(_ => Dispatcher.UIThread.Post(() =>
        {
            scheduled = false;
            if (!this.IsAttachedToVisualTree() || !pendingSections.TryDequeue(out var build)) return;
            Children.Add(build());
            Schedule();
        }, DispatcherPriority.Background));
    }
}
