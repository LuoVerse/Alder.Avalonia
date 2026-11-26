using System.Windows.Input;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class DisclosureGroupViewModel : GalleryViewModel
{
    private static readonly (string Key, string Label)[] Sections =
    [
        ("preview", "DisclosureDesktopPreview"),
        ("notes", "DisclosureReleaseNotes")
    ];

    public IReadOnlyList<string> ExpandedKeys
    {
        get => field;
        set
        {
            if (field.SequenceEqual(value)) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ExpandedSummary));
        }
    } = ["preview"];

    public string ExpandedSummary
    {
        get
        {
            var label = Sections.FirstOrDefault(section => ExpandedKeys.Contains(section.Key)).Label;
            return label is null ? Text("AccordionNone") : Format("AccordionExpanded", Text(label));
        }
    }

    public ICommand Previous { get; }
    public ICommand Next { get; }
    public ICommand CollapseAll { get; }
    public ICommand Reset { get; }

    public DisclosureGroupViewModel()
    {
        Previous = ReactiveCommand.Create(() => Step(-1));
        Next = ReactiveCommand.Create(() => Step(1));
        CollapseAll = ReactiveCommand.Create(() => { ExpandedKeys = []; });
        Reset = ReactiveCommand.Create(() => { ExpandedKeys = [Sections[0].Key]; });
    }

    private void Step(int direction)
    {
        int index = Array.FindIndex(Sections, section => ExpandedKeys.Contains(section.Key));
        if (index < 0) index = direction > 0 ? -1 : 0;
        ExpandedKeys = [Sections[(index + direction + Sections.Length) % Sections.Length].Key];
    }
}
