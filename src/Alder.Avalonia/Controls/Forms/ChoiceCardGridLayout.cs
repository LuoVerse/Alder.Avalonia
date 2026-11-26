using Avalonia;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

internal sealed class ChoiceCardGridLayout
{
    private int measuredColumns;
    private double[] rowHeights = [];

    public Size Measure(IEnumerable<Control> children, Size availableSize, int columns, double minimumWidth, double spacing)
    {
        var cards = children.Where(card => card.IsVisible).ToArray();
        if (cards.Length == 0)
        {
            measuredColumns = 0;
            return default;
        }

        measuredColumns = Math.Clamp(columns, 1, cards.Length);
        var minimum = double.IsFinite(minimumWidth) ? Math.Max(1, minimumWidth) : 156;
        if (double.IsFinite(availableSize.Width))
        {
            var fitting = Math.Floor((availableSize.Width + spacing) / (minimum + spacing));
            measuredColumns = (int)Math.Clamp(fitting, 1, measuredColumns);
        }

        var width = ColumnWidth(availableSize.Width, spacing);
        var rows = (cards.Length - 1) / measuredColumns + 1;
        if (rowHeights.Length != rows) rowHeights = new double[rows];
        else Array.Clear(rowHeights);

        var desiredWidth = 0d;
        for (int index = 0; index < cards.Length; index++)
        {
            var card = cards[index];
            card.Measure(new Size(width, double.PositiveInfinity));
            var row = index / measuredColumns;
            rowHeights[row] = Math.Max(rowHeights[row], card.DesiredSize.Height);
            desiredWidth = Math.Max(desiredWidth, card.DesiredSize.Width);
        }

        return new Size(desiredWidth * measuredColumns + spacing * (measuredColumns - 1),
            rowHeights.Sum() + spacing * (rows - 1));
    }

    public Size Arrange(IEnumerable<Control> children, Size finalSize, double spacing)
    {
        if (measuredColumns == 0) return finalSize;
        var width = ColumnWidth(finalSize.Width, spacing);
        var index = 0;
        var top = 0d;
        foreach (var card in children)
        {
            if (!card.IsVisible) continue;
            var column = index % measuredColumns;
            var rowHeight = rowHeights[index / measuredColumns];
            card.Arrange(new Rect(column * (width + spacing), top, width, rowHeight));
            index++;
            if (index % measuredColumns == 0) top += rowHeight + spacing;
        }
        return finalSize;
    }

    private double ColumnWidth(double width, double spacing) =>
        Math.Max(0, (width - spacing * (measuredColumns - 1)) / measuredColumns);
}
