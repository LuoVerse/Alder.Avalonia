using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class TransitioningContentViewModel
{
    public TransitioningContentSample CrossFade { get; } = new();
    public TransitioningContentSample Slide { get; } = new();
    public TransitioningContentSample Rise { get; } = new();
    public TransitioningContentSample Scale { get; } = new();
    public TransitioningContentSample Blur { get; } = new();
    public TransitioningContentSample Reveal { get; } = new();
}

public sealed class TransitioningContentSample : ReactiveObject
{
    private static readonly string[] PageKeys = ["overview", "activity", "versions"];
    private static readonly TransitioningContentPage[] Pages =
    [
        new("TransitionWorkspaceTitle", "TransitionWorkspaceDetail", "AlderIconChart",
            new("TransitionTasksFinished", "18"), new("TransitionTasksReview", "4"), new("TransitionTasksDrafts", "2")),
        new("TransitionActivityTitle", "TransitionActivityDetail", "AlderIconClock",
            new("TransitionUpdatedSpec", "09:42"), new("TransitionReviewedDesign", "09:16"), new("TransitionExportedFiles", "08:57")),
        new("TransitionVersionsTitle", "TransitionVersionsDetail", "AlderIconArchive",
            new("TransitionVersionCurrent", "v1.4.0"), new("TransitionVersionPatch", "v1.3.2"), new("TransitionVersionInitial", "v1.3.1"))
    ];

    private string selectedPage = "overview";
    private bool isReversed;
    private bool slowMotion;
    private bool reducedMotion;

    public string SelectedPage
    {
        get => selectedPage;
        set
        {
            var index = Array.IndexOf(PageKeys, value);
            if (index < 0 || value == selectedPage) return;

            this.RaiseAndSetIfChanged(ref isReversed, index < Array.IndexOf(PageKeys, selectedPage), nameof(IsReversed));
            this.RaiseAndSetIfChanged(ref selectedPage, value);
            this.RaisePropertyChanged(nameof(Page));
        }
    }

    public TransitioningContentPage Page => Pages[Array.IndexOf(PageKeys, selectedPage)];
    public bool IsReversed => isReversed;

    public bool SlowMotion
    {
        get => slowMotion;
        set
        {
            this.RaiseAndSetIfChanged(ref slowMotion, value);
            this.RaisePropertyChanged(nameof(Duration));
        }
    }

    public TimeSpan? Duration => SlowMotion ? TimeSpan.FromSeconds(1) : null;

    public bool ReducedMotion
    {
        get => reducedMotion;
        set => this.RaiseAndSetIfChanged(ref reducedMotion, value);
    }
}

public sealed class TransitioningContentPage(
    string titleKey, string detailKey, string iconKey, params TransitioningContentRow[] rows) : GalleryViewModel
{
    public string Title => Text(titleKey);
    public string Detail => Text(detailKey);
    public string IconKey => iconKey;
    public IReadOnlyList<TransitioningContentRow> Rows => rows;
}

public sealed class TransitioningContentRow(string labelKey, string value) : GalleryViewModel
{
    public string Label => Text(labelKey);
    public string Value => value;
}
