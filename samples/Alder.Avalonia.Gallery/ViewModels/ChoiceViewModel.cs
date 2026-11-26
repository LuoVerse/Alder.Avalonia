using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class ChoiceViewModel : ReactiveObject
{
    public IReadOnlyList<string> Audience
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = ["team"];
}
