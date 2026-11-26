using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Localization;
using Alder.Avalonia.Gallery.ViewModels;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Gallery.Views;

public partial class PromptInputExample : UserControl
{
    public PromptInputExample() => InitializeComponent();

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (AttachmentComposerHost.DataContext is ChatAttachmentViewModel attachments) attachments.ClearFiles();
        base.OnDetachedFromVisualTree(e);
    }

    private static AiExampleViewModel? QueueOwner(Control c) => c.FindAncestorOfType<ItemsControl>()?.DataContext as AiExampleViewModel;

    private void StartDrag(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: AiQueuedPrompt item } grip &&
            grip.FindAncestorOfType<ItemsControl>() is { DataContext: AiExampleViewModel model } list)
            BeginQueueDrag(grip, model, item, list, e);
    }

    public static void BeginQueueDrag(Border grip, AiExampleViewModel model, AiQueuedPrompt item, ItemsControl list, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed || grip.FindAncestorOfType<Border>() is not { } surface) return;
        int from = model.Queue.IndexOf(item);
        if (from < 0) return;

        var rows = Enumerable.Range(0, model.Queue.Count).Select(list.ContainerFromIndex).ToArray();
        if (rows[from] is not { } source) return;
        var top = rows.Select(row => row?.Bounds.Y ?? 0).ToArray();
        var transforms = new TranslateTransform?[rows.Length];
        double start = e.GetPosition(list).Y;
        double height = source.Bounds.Height;
        var background = surface.Background;
        var zIndex = source.ZIndex;
        int target = from;
        bool dragging = false;
        grip.PointerMoved += Move;
        grip.PointerReleased += Release;
        grip.PointerCaptureLost += Cancel;
        e.Pointer.Capture(grip);
        e.Handled = true;

        void Move(object? sender, PointerEventArgs args)
        {
            double motion = args.GetPosition(list).Y - start;
            if (!dragging)
            {
                if (Math.Abs(motion) < 4) return;
                dragging = true;
                source.ZIndex = zIndex + 1;
                surface.Background = list.FindResource("AlderSurface") as IBrush;
                bool reduced = AlderMotion.GetReduced(list);
                for (int i = 0; i < rows.Length; i++)
                {
                    if (rows[i] is not { } row) continue;
                    var transform = new TranslateTransform();
                    if (i != from && !reduced)
                        transform.Transitions = new Transitions { new DoubleTransition { Property = TranslateTransform.YProperty,
                            Duration = TimeSpan.FromMilliseconds(150), Easing = new CubicEaseOut() } };
                    row.RenderTransform = transforms[i] = transform;
                }
            }

            double y = Math.Clamp(motion, -top[from], Math.Max(0, list.Bounds.Height - top[from] - height));
            transforms[from]!.Y = y;
            int next = from;
            double nearest = Math.Abs(y);
            for (int i = 0; i < top.Length; i++)
            {
                if (rows[i] is null || Math.Abs(top[from] + y - top[i]) >= nearest) continue;
                nearest = Math.Abs(top[from] + y - top[i]);
                next = i;
            }
            if (target != next)
            {
                target = next;
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (i == from || transforms[i] is not { } transform) continue;
                    transform.Y = i > from && i <= target ? -height : i >= target && i < from ? height : 0;
                }
            }
            args.Handled = true;
        }

        void Reset()
        {
            grip.PointerMoved -= Move;
            grip.PointerReleased -= Release;
            grip.PointerCaptureLost -= Cancel;
            if (!dragging) return;
            source.ZIndex = zIndex;
            surface.Background = background;
            for (int i = 0; i < transforms.Length; i++)
                if (transforms[i] is not null) rows[i]?.ClearValue(Visual.RenderTransformProperty);
        }

        void Release(object? sender, PointerReleasedEventArgs args)
        {
            Reset();
            args.Pointer.Capture(null);
            int current = model.Queue.IndexOf(item);
            if (dragging && current == from && target != from && target < model.Queue.Count) model.Queue.Move(from, target);
            args.Handled = true;
        }

        void Cancel(object? sender, PointerCaptureLostEventArgs args) => Reset();
    }

    private void MoveQueueItem(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Up or Key.Down) || sender is not Border { DataContext: AiQueuedPrompt item } grip ||
            QueueOwner(grip) is not { } model) return;
        int from = model.Queue.IndexOf(item);
        int to = from + (e.Key == Key.Up ? -1 : 1);
        if (from >= 0 && to >= 0 && to < model.Queue.Count) model.Queue.Move(from, to);
        e.Handled = true;
    }

    private void RemoveItem(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: AiQueuedPrompt item } button) QueueOwner(button)?.Queue.Remove(item);
    }

    private void OpenQueueMenu(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AiQueuedPrompt item } button || QueueOwner(button) is not { } model) return;
        ShowQueueMenu(model, item, button);
    }

    public static void ShowQueueMenu(AiExampleViewModel model, AiQueuedPrompt item, Button button)
    {
        var menu = new AlderMenuFlyout();
        void Add(string key, Action<int> action, bool enabled = true)
        {
            var entry = new AlderMenuItem { Header = GalleryLocalization.Current.Text(key), IsEnabled = enabled };
            entry.Click += (_, _) => { int index = model.Queue.IndexOf(item); if (index >= 0) action(index); };
            menu.Items.Add(entry);
        }
        int index = model.Queue.IndexOf(item);
        Add("AiEditQueued", _ => model.EditQueued(item));
        Add("AiDuplicateQueued", i => model.Queue.Insert(i + 1, new AiQueuedPrompt(item.Text, item.Attachment)));
        Add("AiQueueUp", i => model.Queue.Move(i, Math.Max(0, i - 1)), index > 0);
        Add("AiQueueDown", i => model.Queue.Move(i, Math.Min(model.Queue.Count - 1, i + 1)), index + 1 < model.Queue.Count);
        Add("AiQueueRemove", model.Queue.RemoveAt);
        menu.ShowAt(button);
    }
}
