using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.Views;

public partial class SearchFieldExample : UserControl
{
    private readonly KeyBinding shortcut;
    private TopLevel? topLevel;

    public SearchFieldExample()
    {
        InitializeComponent();
        shortcut = new KeyBinding
        {
            Gesture = new KeyGesture(Key.K, OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control),
            Command = ReactiveCommand.Create(() =>
            {
                if (ShortcutSearch.IsEffectivelyVisible) ShortcutSearch.Focus();
            })
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        topLevel = TopLevel.GetTopLevel(this);
        topLevel?.KeyBindings.Add(shortcut);
        topLevel?.AddHandler(KeyDownEvent, OnShortcutKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        topLevel?.KeyBindings.Remove(shortcut);
        topLevel?.RemoveHandler(KeyDownEvent, OnShortcutKeyDown);
        topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnShortcutKeyDown(object? sender, KeyEventArgs e)
    {
        if (!KeyboardSearch.IsEffectivelyVisible || topLevel?.FocusManager is not { } focus)
            return;

        var focused = focus.GetFocusedElement();
        if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Shift && focused is not TextBox)
        {
            e.Handled = KeyboardSearch.Focus();
        }
        else if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && KeyboardSearch.IsKeyboardFocusWithin &&
                 string.IsNullOrEmpty(KeyboardSearch.Text) &&
                 !KeyboardSearch.GetVisualDescendants().OfType<TextPresenter>().Any(text => !string.IsNullOrEmpty(text.PreeditText)))
        {
            e.Handled = focus.Focus(null);
        }
    }
}
