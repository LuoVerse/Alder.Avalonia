using System.Collections.ObjectModel;
using System.Windows.Input;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class TagViewModel : GalleryViewModel
{
    private static readonly string[] TopicKeys =
        ["TagDesign", "TagEngineering", "TagAccessibility", "TagPhotography", "TagTravel", "TagBooks"];

    public ObservableCollection<TagExampleItem> Topics { get; } = [];
    public ObservableCollection<TagExampleItem> SelectedTopics { get; } = [];
    public ICommand RemoveTopicCommand { get; }
    public ICommand ResetTopicsCommand { get; }

    public TagViewModel()
    {
        ResetTopics();
        RemoveTopicCommand = ReactiveCommand.Create<TagExampleItem>(item => { Topics.Remove(item); });
        ResetTopicsCommand = ReactiveCommand.Create(ResetTopics);
    }

    private void ResetTopics()
    {
        SelectedTopics.Clear();
        Topics.Clear();
        foreach (var key in TopicKeys)
            Topics.Add(new TagExampleItem(key));
        SelectedTopics.Add(Topics[0]);
    }
}

public sealed class TagExampleItem(string key) : GalleryViewModel
{
    public string Label => Text(key);
    public override string ToString() => Label;
}
