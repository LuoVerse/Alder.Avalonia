using System.Windows.Input;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed record BreadcrumbExampleStep(string Label, int Level, ICommand Navigate);

public sealed class BreadcrumbsViewModel : GalleryViewModel
{
    private static readonly string[] Levels = ["BreadcrumbHome", "BreadcrumbProducts", "BreadcrumbElectronics", "BreadcrumbLaptop"];

    public int CurrentLevel
    {
        get => field;
        private set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Trail));
            this.RaisePropertyChanged(nameof(Location));
        }
    } = Levels.Length - 1;

    public ICommand Navigate { get; }
    public ICommand PreviewNavigate { get; }
    public ICommand RestorePath { get; }

    private string? PreviewLocationKey
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value, nameof(PreviewLocation));
    }

    public IReadOnlyList<BreadcrumbExampleStep> Trail =>
        Levels.Take(CurrentLevel + 1).Select((key, index) => new BreadcrumbExampleStep(Text(key), index, Navigate)).ToArray();

    public string Location => Format("BreadcrumbNavigate", Text(Levels[CurrentLevel]));
    public string? PreviewLocation => PreviewLocationKey is { } key ? Format("BreadcrumbNavigate", Text(key)) : null;

    public BreadcrumbsViewModel()
    {
        Navigate = ReactiveCommand.Create<int>(level => CurrentLevel = level);
        PreviewNavigate = ReactiveCommand.Create<string>(key => PreviewLocationKey = key);
        RestorePath = ReactiveCommand.Create(() => CurrentLevel = Levels.Length - 1);
    }
}
