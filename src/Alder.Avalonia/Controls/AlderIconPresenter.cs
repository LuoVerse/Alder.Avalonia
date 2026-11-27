using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

// The component sizes the slot. The viewbox fits native controls without writing
// to their Width, Height, FontSize, Foreground, or DataContext.
internal sealed class AlderIconPresenter : TemplatedControl
{
    public static readonly StyledProperty<object?> ContentProperty =
        ContentControl.ContentProperty.AddOwner<AlderIconPresenter>();

    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        ContentControl.ContentTemplateProperty.AddOwner<AlderIconPresenter>();

    public static readonly StyledProperty<ControlTheme?> VectorThemeProperty =
        AvaloniaProperty.Register<AlderIconPresenter, ControlTheme?>(nameof(VectorTheme));

    public static readonly DirectProperty<AlderIconPresenter, object?> PresentedContentProperty =
        AvaloniaProperty.RegisterDirect<AlderIconPresenter, object?>(nameof(PresentedContent), p => p.PresentedContent);

    public static IValueConverter SizeToExtent { get; } =
        new FuncValueConverter<AlderSize, double>(size => size switch
        {
            AlderSize.Small => 14,
            AlderSize.Large => 20,
            _ => 16
        });

    public static IValueConverter OptionalVectorTheme { get; } =
        new FuncValueConverter<ControlTheme?, object>(theme => (object?)theme ?? AvaloniaProperty.UnsetValue);

    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    public IDataTemplate? ContentTemplate { get => GetValue(ContentTemplateProperty); set => SetValue(ContentTemplateProperty, value); }

    public ControlTheme? VectorTheme { get => GetValue(VectorThemeProperty); set => SetValue(VectorThemeProperty, value); }

    public object? PresentedContent { get => field; private set => SetAndRaise(PresentedContentProperty, ref field, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty || change.Property == ContentTemplateProperty) RefreshContent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshContent();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Item containers and closed overlays can outlive their visual attachment.
        // Release the control so the same item can be shown again in a new container.
        if (PresentedContent is Control) PresentedContent = null;
        base.OnDetachedFromVisualTree(e);
    }

    internal bool IsBorrowingControl { get; init; }

    private void RefreshContent()
    {
        var content = Content;
        if (content is Control control)
        {
            if (!this.IsAttachedToVisualTree())
                content = null;
            else if (control.FindAncestorOfType<AlderIconPresenter>() is { IsBorrowingControl: true } owner && owner != this)
                owner.Content = new Border { Width = owner.Width, Height = owner.Height, Background = new VisualBrush(control) };
        }
        PresentedContent = ContentTemplate is null ? AsGeometry(content) ?? content : content;
    }

    internal static Geometry? AsGeometry(object? icon)
    {
        if (icon is Geometry geometry) return geometry;
        if (icon is not string text) return null;
        var data = text.AsSpan().Trim();
        if (data.Length > 2 && data[0] is 'F' or 'f' && data[1] is '0' or '1')
            data = data[2..].TrimStart();
        if (data.Length < 2 || data[0] is not ('M' or 'm') ||
            !(char.IsAsciiDigit(data[1]) || char.IsWhiteSpace(data[1]) || data[1] is '-' or '+' or '.'))
            return null;
        try { return Geometry.Parse(text); }
        catch (FormatException) { return null; }
        catch (InvalidOperationException) { return null; }
    }
}
