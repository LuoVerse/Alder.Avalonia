using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Alder.Avalonia.Gallery.Views;

public partial class KanbanExample : UserControl
{
    public static IValueConverter NotionColumnBackground { get; } =
        new FuncValueConverter<IBrush, IBrush?>(brush => WithOpacity(brush, .08));
    public static IValueConverter NotionButtonOutline { get; } =
        new FuncValueConverter<IBrush, IBrush?>(brush => WithOpacity(brush, .3));

    public KanbanExample() => InitializeComponent();

    private static IBrush? WithOpacity(IBrush? brush, double opacity) =>
        brush is ISolidColorBrush solid ? new SolidColorBrush(solid.Color, opacity) : brush;
}
