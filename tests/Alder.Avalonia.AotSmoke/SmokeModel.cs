using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Media;
using Alder.Avalonia.Controls;

namespace Alder.Avalonia.AotSmoke;

public sealed record SmokeItem(string Key, string Label, string Body);

public sealed class SmokeModel : INotifyPropertyChanged
{
    public Geometry Icon { get; } = StreamGeometry.Parse("M0,5 L3,8 L9,0");
    public string Description { get; } = "Compiled description";
    public string Prefix { get; } = "https://";
    public bool Required { get; } = true;
    public int Rows { get; } = 3;
    public int Activations { get; private set; }
    public ICommand Activate { get; }
    public ICommand CloseMorphTab { get; }
    public ICommand MoveMorphTab { get; }

    public ObservableCollection<SmokeItem> Items { get; } =
        [new("first", "First", "First body"), new("second", "Second", "Second body")];
    public ObservableCollection<SmokeItem> MorphItems { get; } =
        [new("overview", "Overview", "Overview body"), new("activity", "Activity", "Activity body"), new("notes", "Notes", "Notes body")];
    public string Text { get => field; set => Set(ref field, value); } = "Initial text";
    public string Code { get => field; set => Set(ref field, value); } = "123";
    public bool? Checked { get => field; set => Set(ref field, value); } = false;
    public bool Invalid { get => field; set => Set(ref field, value); }
    public bool Expanded { get => field; set => Set(ref field, value); }
    public decimal? Number { get => field; set => Set(ref field, value); } = 12m;
    public double Position { get => field; set => Set(ref field, value); } = 20;
    public int Page { get => field; set => Set(ref field, value); } = 5;
    public IReadOnlyList<AlderPaginationItem>? PageItems { get => field; set => Set(ref field, value); }
    public AlderTabAlignment TabAlignment { get => field; set => Set(ref field, value); }
    public int SelectedTab { get => field; set => Set(ref field, value); }
    public int SelectedExpandableTab { get => field; set => Set(ref field, value); } = -1;
    public SmokeItem? SelectedMorphTab { get => field; set => Set(ref field, value); }
    public DateOnly? Date { get => field; set => Set(ref field, value); } = new DateOnly(2026, 11, 10);
    public TimeOnly? Time { get => field; set => Set(ref field, value); } = new TimeOnly(9, 30);
    public AlderDateRange? Dates { get => field; set => Set(ref field, value); } =
        new AlderDateRange(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 12));
    public IReadOnlyList<string> SelectedKeys { get => field; set => Set(ref field, value); } = ["first"];
    public IReadOnlyList<string>? ExpandedKeys { get => field; set => Set(ref field, value); } = ["first"];
    public IReadOnlyList<AlderPickerOption> PickerChoices { get; } =
    [
        new() { Text = "Alpha" }, new() { Text = "Archived", IsEnabled = false }, new() { Text = "Bravo" }
    ];
    public IList PickerSelections { get; } = new ObservableCollection<object>();
    public object? PickerChoice { get => field; set => Set(ref field, value); }
    public string? PickerText { get => field; set => Set(ref field, value); }
    public int HeadingLevel { get => field; set => Set(ref field, value); } = 2;
    public KbdKey ShortcutKey { get => field; set => Set(ref field, value); } = KbdKey.Command;
    public Func<object, object> TableRowKey { get; } = row => ((SmokeItem)row).Key;
    public IReadOnlyCollection<object> TableSelection { get; } = ["first"];
    public ScrollShadowVisibility ShadowVisibility { get => field; set => Set(ref field, value); }

    public SmokeModel()
    {
        Activate = new SmokeCommand(_ => Activations++);
        CloseMorphTab = new SmokeCommand(parameter =>
        {
            if (parameter is SmokeItem tab) MorphItems.Remove(tab);
        });
        MoveMorphTab = new SmokeCommand(parameter =>
        {
            if (parameter is AlderTabReorderRequest move) MorphItems.Move(move.OldIndex, move.NewIndex);
        });
        SelectedMorphTab = MorphItems[0];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private sealed class SmokeCommand(Action<object?> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter);
    }
}
