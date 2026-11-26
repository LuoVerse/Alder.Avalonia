using System.Collections;
using System.Windows.Input;
using Avalonia.Collections;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed record SelectExampleUser(string Name, string Email, string Initials, IImage Photo);

public sealed class SelectViewModel : GalleryViewModel
{
    private const int InitialClearableStateIndex = 2;
    private static readonly string[] StateKeys =
        ["PickerFlorida", "PickerDelaware", "PickerCalifornia", "PickerTexas", "PickerNewYork", "PickerWashington"];
    private static readonly string[] ControlledStateKeys =
        ["PickerCalifornia", "PickerTexas", "PickerFlorida", "PickerNewYork", "PickerIllinois", "PickerPennsylvania"];
    private static readonly string[] Pokemon =
    [
        "bulbasaur", "ivysaur", "venusaur", "charmander", "charmeleon", "charizard", "squirtle", "wartortle",
        "blastoise", "caterpie", "metapod", "butterfree", "weedle", "kakuna", "beedrill", "pidgey", "pidgeotto",
        "pidgeot", "rattata", "raticate", "spearow", "fearow", "ekans", "arbok", "pikachu", "raichu", "sandshrew",
        "sandslash", "nidoran-f", "nidorina", "nidoqueen", "nidoran-m", "nidorino", "nidoking", "clefairy", "clefable",
        "vulpix", "ninetales", "jigglypuff", "wigglytuff", "zubat", "golbat", "oddish", "gloom", "vileplume", "paras",
        "parasect", "venonat", "venomoth", "diglett", "dugtrio", "meowth", "persian", "psyduck", "golduck", "mankey",
        "primeape", "growlithe", "arcanine", "poliwag"
    ];

    private readonly AvaloniaList<object> selectedStates = new() { ControlledStateKeys[0], ControlledStateKeys[1] };

    public SelectViewModel()
    {
        selectedStates.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(MultipleStatus));
        ToggleOpen = ReactiveCommand.Create(() => IsOpen = !IsOpen);
        ResetClearableState = ReactiveCommand.Create(() => { ClearableStateIndex = InitialClearableStateIndex; });
        ResetSelectedStates = ReactiveCommand.Create(() =>
        {
            selectedStates.Clear();
            selectedStates.AddRange(ControlledStateKeys.Take(2));
        });

        LoadMore = ReactiveCommand.CreateFromTask(async () =>
        {
            if (LoadedPokemon.Count >= Pokemon.Length) return;
            IsLoading = true;
            try
            {
                await Task.Delay(280);
                LoadedPokemon.AddRange(Pokemon.Skip(LoadedPokemon.Count).Take(20));
            }
            finally { IsLoading = false; }
        });
    }

    public IReadOnlyList<string> StateOptions => StateKeys;
    public IReadOnlyList<string> DisplayStates => Labels(StateKeys);
    public IReadOnlyList<string> ControlledStates => ControlledStateKeys;
    public Func<object?, string?> Caption
    {
        get
        {
            var localizedStates = StateKeys.Concat(ControlledStateKeys).Distinct().ToDictionary(key => key, Text);
            return item => item is string key ? localizedStates.GetValueOrDefault(key) : null;
        }
    }

    public IReadOnlyList<string> Countries => Labels("PickerArgentina", "PickerVenezuela", "PickerJapan", "PickerFrance",
        "PickerItaly", "PickerSpain", "PickerThailand", "PickerNewZealand", "PickerIceland");
    public IList DisabledCountries => new AvaloniaList<object>
        { Text("PickerArgentina"), Text("PickerJapan"), Text("PickerFrance") };
    public IReadOnlyList<string> Animals => Labels("PickerDog", "PickerCat", "AutocompleteBird", "PickerKangaroo",
        "PickerElephant", "PickerTiger");
    public IReadOnlyList<AlderPickerOption> DisabledAnimals => Animals
        .Select((animal, index) => new AlderPickerOption { Text = animal, IsEnabled = index is not (1 or 3) }).ToArray();

    public IReadOnlyList<AlderPickerOption> CountrySections =>
    [
        SectionOption("AutocompleteUnitedStates", "AutocompleteNorthAmerica"),
        SectionOption("AutocompleteCanada", "AutocompleteNorthAmerica"),
        SectionOption("AutocompleteMexico", "AutocompleteNorthAmerica"),
        SectionOption("AutocompleteUnitedKingdom", "AutocompleteEurope"),
        SectionOption("PickerFrance", "AutocompleteEurope"),
        SectionOption("AutocompleteGermany", "AutocompleteEurope"),
        SectionOption("PickerSpain", "AutocompleteEurope"),
        SectionOption("PickerItaly", "AutocompleteEurope"),
        SectionOption("PickerJapan", "AutocompleteAsia"),
        SectionOption("AutocompleteChina", "AutocompleteAsia"),
        SectionOption("AutocompleteIndia", "AutocompleteAsia"),
        SectionOption("AutocompleteSouthKorea", "AutocompleteAsia")
    ];

    public IReadOnlyList<string> Options => Enumerable.Range(1, 2)
        .Select(index => Format("AutocompleteOptionFormat", index)).ToArray();

    public IReadOnlyList<string> Plans => Labels("SelectFree", "SelectPro");
    public IReadOnlyList<SelectExampleUser> Users { get; } = CreateUsers();

    internal static IReadOnlyList<SelectExampleUser> CreateUsers() =>
    [
        Person("Bob", "Card/blue"),
        Person("Fred", "Avatar/green"),
        Person("Martha", "Avatar/purple"),
        Person("John", "Card/red"),
        Person("Jane", "Avatar/orange")
    ];

    public static Func<object?, string?> UserCaption { get; } = item => (item as SelectExampleUser)?.Name;
    public AvaloniaList<string> LoadedPokemon { get; } = [];
    public IList SelectedStates => selectedStates;
    public SelectFormViewModel RequiredForm { get; } = new();
    public SelectFormViewModel SurfaceForm { get; } = new();
    public ICommand ToggleOpen { get; }
    public ICommand LoadMore { get; }
    public ICommand ResetClearableState { get; }
    public ICommand ResetSelectedStates { get; }
    public int ClearableStateIndex { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = InitialClearableStateIndex;

    public string SelectedStatus => Format("AutocompleteSelectedFormat", Text(SelectedState as string ?? "FeedbackNone"));
    public string MultipleStatus => Format("AutocompleteSelectedFormat", selectedStates.Count == 0
        ? Text("FeedbackNone") : string.Join(DisplayCulture.TextInfo.ListSeparator + " ", selectedStates.OfType<string>().Select(Text)));
    public string OpenButton => Text(IsOpen ? "SelectClose" : "SelectOpen");
    public string OpenStatus => Text(IsOpen ? "SelectIsOpen" : "SelectIsClosed");

    public object? SelectedState
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(SelectedStatus));
        }
    } = ControlledStateKeys[0];

    public bool IsOpen
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(OpenButton));
            this.RaisePropertyChanged(nameof(OpenStatus));
        }
    }

    public bool IsLoading
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    private IReadOnlyList<string> Labels(params string[] keys) => keys.Select(Text).ToArray();

    private AlderPickerOption SectionOption(string key, string group) => new() { Text = Text(key), Group = Text(group) };

    private static SelectExampleUser Person(string name, string asset)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://Alder.Avalonia.Gallery/Assets/{asset}.jpg"));
        return new(name, $"{name.ToLowerInvariant()}@alder.example", name[..1], new Bitmap(stream));
    }
}


public sealed class SelectFormViewModel : ReactiveObject
{
    private bool submitted;

    public SelectFormViewModel() => Submit = ReactiveCommand.Create(() =>
    {
        submitted = true;
        this.RaisePropertyChanged(nameof(IsStateInvalid));
        this.RaisePropertyChanged(nameof(IsCountryInvalid));
    });

    public ICommand Submit { get; }
    public bool IsStateInvalid => submitted && State is null;
    public bool IsCountryInvalid => submitted && Country is null;

    public object? State
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsStateInvalid));
        }
    }

    public object? Country
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsCountryInvalid));
        }
    }
}
