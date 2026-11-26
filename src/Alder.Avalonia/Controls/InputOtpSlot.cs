using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Alder.Avalonia.Controls;

internal sealed class InputOtpSlot : TemplatedControl
{
    public static readonly StyledProperty<string> GlyphProperty =
        AvaloniaProperty.Register<InputOtpSlot, string>(nameof(Glyph), "");

    public string Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public void Update(string glyph, bool filled, bool active, bool invalid, bool secondary)
    {
        Glyph = glyph;
        PseudoClasses.Set(":filled", filled);
        PseudoClasses.Set(":active", active);
        PseudoClasses.Set(":invalid", invalid);
        PseudoClasses.Set(":secondary", secondary);
    }
}
