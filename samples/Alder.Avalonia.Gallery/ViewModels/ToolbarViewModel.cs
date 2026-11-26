using System.Windows.Input;
using Avalonia.Media;
using Alder.Avalonia.Gallery.Localization;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class ToolbarViewModel : GalleryViewModel
{
    private readonly Stack<EditState> undo = new();
    private readonly Stack<EditState> redo = new();
    private sealed record EditState(string? Sample, TextAlignment Alignment);

    private EditState State
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Sample));
            this.RaisePropertyChanged(nameof(Alignment));
        }
    } = new(null, TextAlignment.Left);

    public ToolbarViewModel() => Edit = ReactiveCommand.Create<string?>(Apply);

    public ICommand Edit { get; }
    public string Sample => State.Sample ?? Text("TheQuickBrownFoxJumpsOverTheLazyDog");
    public TextAlignment Alignment => State.Alignment;
    public string Status => Display(StatusMessage);

    private GalleryMessage StatusMessage
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value, nameof(Status));
    } = "UseAToolbarToFormatTheSample";
    public bool Bold => Formatting.Contains("Bold");
    public bool Italic => Formatting.Contains("Italic");
    public bool Underline => Formatting.Contains("Underline");

    public IReadOnlyList<string> Formatting
    {
        get => field;
        set
        {
            if (field.SequenceEqual(value)) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Bold));
            this.RaisePropertyChanged(nameof(Italic));
            this.RaisePropertyChanged(nameof(Underline));
        }
    } = [];

    public void Copied(bool cut, string copiedText)
    {
        bool removed = cut && Sample == copiedText;
        if (removed) Commit(State with { Sample = "" });
        StatusMessage = removed ? "SampleCut" : "SampleCopied";
    }

    public void ClipboardUnavailable() => StatusMessage = "ClipboardIsUnavailable";

    private void Apply(string? action)
    {
        switch (action)
        {
            case "Undo" when undo.TryPop(out var previous):
                redo.Push(State);
                State = previous;
                StatusMessage = "EditUndone";
                break;
            case "Redo" when redo.TryPop(out var following):
                undo.Push(State);
                State = following;
                StatusMessage = "EditRedone";
                break;
            case "Undo":
                StatusMessage = "NoEditToUndo";
                break;
            case "Redo":
                StatusMessage = "NoEditToRedo";
                break;
            case "Left":
            case "Center":
            case "Right":
                Commit(State with { Alignment = Enum.Parse<TextAlignment>(action) });
                StatusMessage = new GalleryMessage("AlignmentChanged", new GalleryMessage(action));
                break;
        }
    }

    private void Commit(EditState next)
    {
        if (State == next) return;
        undo.Push(State);
        redo.Clear();
        State = next;
    }
}
