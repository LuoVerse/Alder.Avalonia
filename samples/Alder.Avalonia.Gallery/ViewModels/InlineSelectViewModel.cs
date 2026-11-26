using System.Collections;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class InlineSelectViewModel : GalleryViewModel
{
    public InlineSelectViewModel() => SelectedPlacement = Placements[2];

    public IReadOnlyList<string> Languages { get; } = ["ProDisplayEnglish", "ProDisplaySpanish", "FormFrench", "FormJapanese"];

    public IReadOnlyList<InlineSelectTeam> Teams { get; } =
    [
        new("FormNorthwind", "N", new SolidColorBrush(Color.Parse("#7008E7"))),
        new("FormAcme", "A", new SolidColorBrush(Color.Parse("#155DFC"))),
        new("FormMaple", "M", new SolidColorBrush(Color.Parse("#E17100")))
    ];

    public IReadOnlyList<InlineSelectPlacement> Placements { get; } =
    [
        new("InlineSelectBottom", PlacementMode.Bottom),
        new("InlineSelectBottomStart", PlacementMode.BottomEdgeAlignedLeft),
        new("InlineSelectBottomEnd", PlacementMode.BottomEdgeAlignedRight),
        new("InlineSelectTop", PlacementMode.Top),
        new("InlineSelectTopStart", PlacementMode.TopEdgeAlignedLeft),
        new("InlineSelectTopEnd", PlacementMode.TopEdgeAlignedRight),
        new("InlineSelectLeft", PlacementMode.Left),
        new("InlineSelectLeftTop", PlacementMode.LeftEdgeAlignedTop),
        new("InlineSelectLeftBottom", PlacementMode.LeftEdgeAlignedBottom),
        new("InlineSelectRight", PlacementMode.Right),
        new("InlineSelectRightTop", PlacementMode.RightEdgeAlignedTop),
        new("InlineSelectRightBottom", PlacementMode.RightEdgeAlignedBottom)
    ];

    public InlineSelectPlacement? SelectedPlacement
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public IReadOnlyList<string> Notifications { get; } =
        ["InlineSelectEmail", "InlineSelectWhatsApp", "InlineSelectPushNotification"];

    public IList SelectedNotifications { get; } =
        new AvaloniaList<object> { "InlineSelectEmail", "InlineSelectPushNotification" };

    public IReadOnlyList<string> Roles { get; } = ["ProDisplayViewer", "ProDisplayEditor", "InlineSelectAdmin"];

    public Func<object?, string?> ChoiceText
    {
        get
        {
            var captions = Languages.Concat(Notifications).Concat(Roles).ToDictionary(key => key, Text);
            return item => item is string key ? captions[key] : item?.ToString();
        }
    }
}

public sealed class InlineSelectTeam(string labelKey, string initials, IBrush badgeBrush) : GalleryViewModel
{
    public string Title => Text(labelKey);
    public string Initials { get; } = initials;
    public IBrush BadgeBrush { get; } = badgeBrush;
    public override string ToString() => Title;
}

public sealed class InlineSelectPlacement(string labelKey, PlacementMode mode) : GalleryViewModel
{
    public PlacementMode Mode { get; } = mode;
    public override string ToString() => Text(labelKey);
}
