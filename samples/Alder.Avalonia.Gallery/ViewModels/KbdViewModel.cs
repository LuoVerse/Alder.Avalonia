using Alder.Avalonia.Controls;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed record KbdExamplePart(KbdKey Key = KbdKey.None, string? Text = null)
{
    public bool IsKey => Key != KbdKey.None;
}

public sealed record KbdExampleShortcut(string Label, IReadOnlyList<KbdExamplePart> Keys);

public sealed class KbdViewModel : GalleryViewModel
{
    public IReadOnlyList<KbdExampleShortcut> EditingShortcuts =>
    [
        new(Text("KbdCopyAction"), [new(KbdKey.Command), new(Text: "C")]),
        new(Text("KbdPasteAction"), [new(KbdKey.Command), new(Text: "V")]),
        new(Text("KbdCutAction"), [new(KbdKey.Command), new(Text: "X")]),
        new(Text("KbdUndoAction"), [new(KbdKey.Command), new(Text: "Z")]),
        new(Text("KbdRedoAction"), [new(KbdKey.Command), new(KbdKey.Shift), new(Text: "Z")])
    ];

    public IReadOnlyList<KbdExampleShortcut> QuickActions =>
    [
        new(Text("KbdSearchAction"), [new(KbdKey.Command), new(Text: "K")]),
        new(Text("KbdSidebarAction"), [new(KbdKey.Command), new(Text: "B")]),
        new(Text("KbdNewFileAction"), [new(KbdKey.Command), new(Text: "N")]),
        new(Text("KbdSaveAction"), [new(KbdKey.Command), new(Text: "S")])
    ];
}
