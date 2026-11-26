using System.Windows.Input;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class FeedbackViewModel : GalleryViewModel
{
    public FeedbackViewModel()
    {
        DismissAlertCommand = ReactiveCommand.Create(() => { ShowAlert = false; });
        ResetAlertCommand = ReactiveCommand.Create(() => { ShowAlert = true; });
    }

    public ICommand DismissAlertCommand { get; }
    public ICommand ResetAlertCommand { get; }

    public bool ShowAlert
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public bool IsIndeterminate
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public double Work
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 750;

    public double PreviewValue
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 60;

    public AlderSize PreviewSize
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = AlderSize.Medium;

    public FeedbackColor PreviewColor
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = FeedbackColor.Accent;

    public bool PreviewIsIndeterminate
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public SpinnerSize PreviewSpinnerSize
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = SpinnerSize.Medium;

    public IReadOnlyList<FeedbackColor> PreviewColors { get; } = Enum.GetValues<FeedbackColor>();
    public IReadOnlyList<AlderSize> PreviewSizes { get; } = Enum.GetValues<AlderSize>();
    public IReadOnlyList<SpinnerSize> PreviewSpinnerSizes { get; } = Enum.GetValues<SpinnerSize>();

    public Func<object?, string?> PreviewColorCaption => value => value is FeedbackColor color
        ? Text("Feedback" + color) : null;

    public Func<object?, string?> PreviewSizeCaption => value => value switch
    {
        SpinnerSize.ExtraLarge => Text("FeedbackExtraLarge"),
        AlderSize or SpinnerSize => Text(value.ToString()!),
        _ => null
    };

    public IReadOnlyList<SkeletonAnimation> SkeletonAnimationOptions { get; } =
        [SkeletonAnimation.Shimmer, SkeletonAnimation.Pulse, SkeletonAnimation.None];

    public Func<object?, string?> SkeletonAnimationCaption
    {
        get
        {
            var shimmer = Text("FeedbackShimmer");
            var pulse = Text("FeedbackPulse");
            var none = Text("FeedbackNone");
            return item => item switch
            {
                SkeletonAnimation.Shimmer => shimmer,
                SkeletonAnimation.Pulse => pulse,
                SkeletonAnimation.None => none,
                _ => null
            };
        }
    }

    public SkeletonAnimation PreviewSkeletonAnimation
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
}
