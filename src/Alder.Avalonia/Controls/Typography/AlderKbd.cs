using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the visual treatment of a keyboard key cap.</summary>
public enum KbdVariant
{
    /// <summary>Uses the default treatment.</summary>
    Default,
    /// <summary>Uses the lighter key cap treatment.</summary>
    Light
}

/// <summary>Identifies the platform-aware glyph used for a named keyboard key.</summary>
public enum KbdKey
{
    /// <summary>Shows no glyph for a named key.</summary>
    None,
    /// <summary>Represents the Command key.</summary>
    Command,
    /// <summary>Represents the Shift key.</summary>
    Shift,
    /// <summary>Represents the Control key.</summary>
    Ctrl,
    /// <summary>Represents the Option key.</summary>
    Option,
    /// <summary>Represents the Enter key.</summary>
    Enter,
    /// <summary>Represents the Delete key.</summary>
    Delete,
    /// <summary>Represents the Escape key.</summary>
    Escape,
    /// <summary>Represents the Tab key.</summary>
    Tab,
    /// <summary>Represents the Caps Lock key.</summary>
    CapsLock,
    /// <summary>Represents the Up Arrow key.</summary>
    Up,
    /// <summary>Represents the Right Arrow key.</summary>
    Right,
    /// <summary>Represents the Down Arrow key.</summary>
    Down,
    /// <summary>Represents the Left Arrow key.</summary>
    Left,
    /// <summary>Represents the Page Up key.</summary>
    PageUp,
    /// <summary>Represents the Page Down key.</summary>
    PageDown,
    /// <summary>Represents the Home key.</summary>
    Home,
    /// <summary>Represents the End key.</summary>
    End,
    /// <summary>Represents the Help key.</summary>
    Help,
    /// <summary>Represents the Space key.</summary>
    Space,
    /// <summary>Represents the Function key.</summary>
    Fn,
    /// <summary>Represents the Windows key.</summary>
    Win,
    /// <summary>Represents the Alt key.</summary>
    Alt
}

/// <summary>Displays one or more keys as a shortcut. Items may be text, key abbreviations, or custom content.</summary>
public sealed class AlderKbd : ItemsControl
{
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<KbdVariant> VariantProperty =
        AvaloniaProperty.Register<AlderKbd, KbdVariant>(nameof(Variant));

    /// <summary>The visual treatment applied to the control.</summary>
    public KbdVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
}

/// <summary>A standard keyboard abbreviation with a localized accessible name and tooltip.</summary>
public sealed class AlderKbdKey : TemplatedControl
{
    /// <summary>Identifies the <see cref="KeyValue"/> Avalonia property.</summary>
    public static readonly StyledProperty<KbdKey> KeyValueProperty =
        AvaloniaProperty.Register<AlderKbdKey, KbdKey>(nameof(KeyValue));
    /// <summary>Identifies the <see cref="Title"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AlderKbdKey, string?>(nameof(Title));
    /// <summary>Identifies the <see cref="Glyph"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderKbdKey, string?> GlyphProperty =
        AvaloniaProperty.RegisterDirect<AlderKbdKey, string?>(nameof(Glyph), key => key.Glyph);

    /// <summary>The named key to represent with a key cap.</summary>
    public KbdKey KeyValue { get => GetValue(KeyValueProperty); set => SetValue(KeyValueProperty, value); }
    /// <summary>The accessible title for the represented key.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>The visible symbol or label used for the represented key.</summary>
    public string? Glyph { get => field; private set => SetAndRaise(GlyphProperty, ref field, value); }

    private IDisposable? titleBinding;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != KeyValueProperty) return;

        Glyph = KeyValue switch
        {
            KbdKey.Command or KbdKey.Win => "⌘",
            KbdKey.Shift => "⇧",
            KbdKey.Ctrl => "⌃",
            KbdKey.Option or KbdKey.Alt => "⌥",
            KbdKey.Enter => "↵",
            KbdKey.Delete => "⌫",
            KbdKey.Escape => "⎋",
            KbdKey.Tab => "⇥",
            KbdKey.CapsLock => "⇪",
            KbdKey.Up => "↑",
            KbdKey.Right => "→",
            KbdKey.Down => "↓",
            KbdKey.Left => "←",
            KbdKey.PageUp => "⇞",
            KbdKey.PageDown => "⇟",
            KbdKey.Home => "↖",
            KbdKey.End => "↘",
            KbdKey.Help => "?",
            KbdKey.Space => "␣",
            KbdKey.Fn => "Fn",
            _ => null
        };

        titleBinding?.Dispose();
        titleBinding = Glyph is null ? null : Bind(TitleProperty,
            this.GetResourceObservable($"AlderText.Kbd{KeyValue}"), BindingPriority.Style);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new KbdKeyPeer(this);

    private sealed class KbdKeyPeer(AlderKbdKey owner) : ControlAutomationPeer(owner)
    {
        protected override string? GetNameCore() => base.GetNameCore() ?? owner.Title ?? owner.Glyph;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    }
}
