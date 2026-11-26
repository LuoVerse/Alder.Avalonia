using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class ContextMenuViewModel : GalleryViewModel
{
    private static readonly KeyModifiers ShortcutModifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
    private bool menuOpen;

    public ContextMenuViewModel() => ShowMenuActionCommand = ReactiveCommand.Create<string>(action =>
        Toast.Show(new ToastOptions(Format("ContextMenuActionInvoked", action)) { Duration = TimeSpan.FromSeconds(2) }));

    public ICommand ShowMenuActionCommand { get; }

    public bool MenuOpen
    {
        get => menuOpen;
        set
        {
            if (menuOpen == value) return;
            this.RaiseAndSetIfChanged(ref menuOpen, value);
            this.RaisePropertyChanged(nameof(MenuStatus));
        }
    }

    public string MenuStatus => Text(MenuOpen ? "ContextMenuOpen" : "ContextMenuClosed");
    public KeyGesture BackGesture { get; } = new(Key.OemOpenBrackets, ShortcutModifier);
    public KeyGesture ForwardGesture { get; } = new(Key.OemCloseBrackets, ShortcutModifier);
    public KeyGesture ReloadGesture { get; } = new(Key.R, ShortcutModifier);
    public KeyGesture CutGesture { get; } = new(Key.X, ShortcutModifier);
    public KeyGesture CopyGesture { get; } = new(Key.C, ShortcutModifier);
    public KeyGesture PasteGesture { get; } = new(Key.V, ShortcutModifier);
    public KeyGesture SaveGesture { get; } = new(Key.S, ShortcutModifier);
    public KeyGesture BookmarksGesture { get; } = new(Key.B, ShortcutModifier);
}
