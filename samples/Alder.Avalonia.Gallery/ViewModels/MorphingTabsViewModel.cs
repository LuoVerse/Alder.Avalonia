using System.Collections.ObjectModel;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class MorphingTabExampleItem(string titleKey, string bodyKey) : GalleryViewModel
{
    public string Title => Text(titleKey);
    public string Body => Text(bodyKey);
}

public sealed class MorphingTabsViewModel : GalleryViewModel
{
    public ObservableCollection<MorphingTabExampleItem> Tabs { get; } = [];
    public ICommand Reorder { get; }
    public ICommand Close { get; }
    public ICommand Reset { get; }

    public MorphingTabExampleItem? SelectedTab
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsEmpty => Tabs.Count == 0;

    public MorphingTabsViewModel()
    {
        Tabs.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(IsEmpty));
        Reorder = ReactiveCommand.Create<AlderTabReorderRequest>(request =>
        {
            Tabs.Move(request.OldIndex, request.NewIndex);
        });
        Close = ReactiveCommand.Create<MorphingTabExampleItem>(tab =>
        {
            int index = Tabs.IndexOf(tab);
            if (index < 0) return;
            bool wasSelected = SelectedTab == tab;
            Tabs.RemoveAt(index);
            if (wasSelected)
                SelectedTab = Tabs.Count == 0 ? null : Tabs[Math.Min(index, Tabs.Count - 1)];
        });
        Reset = ReactiveCommand.Create(ResetTabs);
        ResetTabs();
    }

    private void ResetTabs()
    {
        Tabs.Clear();
        Tabs.Add(new("TabOverview", "TabOverviewBody"));
        Tabs.Add(new("TabActivity", "TabActivityBody"));
        Tabs.Add(new("TabSettings", "TabSettingsBody"));
        SelectedTab = Tabs[0];
    }
}
