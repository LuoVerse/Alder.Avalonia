using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed record ScrollShadowStory(Bitmap Image, string AlternativeText, string Title, string Time);

public sealed class ScrollShadowViewModel : GalleryViewModel
{
    private static readonly (string File, string Alt)[] StoryAssets =
    [
        ("robot1.jpeg", "CardRobotSideAlt"),
        ("avocado.jpeg", "CardAvocadoAlt"),
        ("oranges.jpeg", "CardOrangesAlt"),
        ("cherries.jpeg", "CardCherriesAlt")
    ];

    private readonly Lazy<Bitmap[]> storyImages = new(() => StoryAssets.Select(asset =>
    {
        using var stream = AssetLoader.Open(new Uri($"avares://Alder.Avalonia.Gallery/Assets/Card/{asset.File}"));
        return Bitmap.DecodeToWidth(stream, 160);
    }).ToArray());

    public IReadOnlyList<string> Paragraphs => Enumerable.Repeat(Text("ScrollShadowParagraph"), 10).ToArray();
    public IReadOnlyList<string> Notes => Enumerable.Range(1, 7).Select(index => Text("ScrollShadowNote" + index)).ToArray();

    public IReadOnlyList<ScrollShadowStory> Stories => Enumerable.Range(0, 8).Select(index =>
    {
        int asset = index % StoryAssets.Length;
        return new ScrollShadowStory(storyImages.Value[asset], Text(StoryAssets[asset].Alt), Text("CardBridging"), Text("CardToday"));
    }).ToArray();

    public string VerticalStatus => Format("ScrollShadowVerticalStatus", StateText(VerticalState));
    public string HorizontalStatus => Format("ScrollShadowHorizontalStatus", StateText(HorizontalState));
    public string TermsDescription => Text(TermsResult ?? "ScrollShadowTermsDescription");

    public ScrollShadowVisibility VerticalState
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(VerticalStatus));
        }
    } = ScrollShadowVisibility.None;

    public ScrollShadowVisibility HorizontalState
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(HorizontalStatus));
        }
    } = ScrollShadowVisibility.None;

    public ScrollShadowVisibility Visibility
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = ScrollShadowVisibility.Auto;

    public bool IsDetectionEnabled
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    private string? TermsResult
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(TermsDescription));
        }
    }

    public ICommand ChangeVisibility { get; }
    public ICommand AcceptTerms { get; }
    public ICommand CancelTerms { get; }

    public ScrollShadowViewModel()
    {
        ChangeVisibility = ReactiveCommand.Create<ScrollShadowVisibility>(visibility => { Visibility = visibility; });
        AcceptTerms = ReactiveCommand.Create(() => { TermsResult = "ScrollShadowTermsAccepted"; });
        CancelTerms = ReactiveCommand.Create(() => { TermsResult = "ScrollShadowTermsCancelled"; });
    }

    private string StateText(ScrollShadowVisibility state) => Text(state switch
    {
        ScrollShadowVisibility.Top => "DialogAtTop",
        ScrollShadowVisibility.Bottom => "DialogAtBottom",
        ScrollShadowVisibility.Left => "ScrollShadowStart",
        ScrollShadowVisibility.Right => "ScrollShadowEnd",
        ScrollShadowVisibility.Both => "ScrollShadowBoth",
        _ => "FeedbackNone"
    });
}
