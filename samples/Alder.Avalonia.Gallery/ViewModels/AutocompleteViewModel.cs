using System.Collections;
using System.Globalization;
using System.Windows.Input;
using Avalonia.Collections;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed record AutocompleteCurrency(string Code, string Name, string Symbol);

public sealed class AutocompleteViewModel : GalleryViewModel
{
    private static readonly string[] Characters =
    [
        "Luke Skywalker", "C-3PO", "R2-D2", "Darth Vader", "Leia Organa", "Obi-Wan Kenobi",
        "Han Solo", "Chewbacca", "Yoda", "Lando Calrissian"
    ];

    private static readonly (string Code, string Symbol)[] CurrencyCodes =
        [("USD", "$"), ("EUR", "€"), ("GBP", "£"), ("JPY", "¥"), ("CHF", "₣")];
    private static readonly string[] StateKeys =
        ["PickerCalifornia", "PickerTexas", "PickerFlorida", "PickerNewYork", "PickerIllinois", "PickerPennsylvania"];

    private readonly AvaloniaList<object> selectedStates;
    private CancellationTokenSource? searchRequest;

    public AutocompleteViewModel()
    {
        SelectedState = StateKeys[0];
        selectedStates = new AvaloniaList<object> { StateKeys[0], StateKeys[1] };
        selectedStates.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(SelectedStatesStatus));
        ToggleOpen = ReactiveCommand.Create(() => IsOpen = !IsOpen);
        Submit = ReactiveCommand.Create(() => FormSubmitted = true);
        ResetSelectedStates = ReactiveCommand.Create(() =>
        {
            selectedStates.Clear();
            selectedStates.AddRange(StateKeys.Take(2));
        });
        ResetTripStates = ReactiveCommand.Create(TripStates.Clear);
        ResetMultipleStates = ReactiveCommand.Create(MultipleStates.Clear);
        ResetVariants = ReactiveCommand.Create(() =>
        {
            PrimarySelection = SecondarySelection = -1;
            PrimaryTags.Clear();
            SecondaryTags.Clear();
        });
    }

    public IReadOnlyList<string> States => Labels("PickerFlorida", "PickerDelaware", "PickerCalifornia",
        "PickerTexas", "PickerNewYork", "PickerWashington");

    public IReadOnlyList<string> AlternateStates => Labels(StateKeys);
    public IReadOnlyList<string> ControlledStates => StateKeys;

    public Func<object?, string?> StateText
    {
        get
        {
            var localizedStates = StateKeys.ToDictionary(key => key, Text);
            return item => item is string key ? localizedStates.GetValueOrDefault(key) : null;
        }
    }

    public IReadOnlyList<string> Countries => Labels("PickerArgentina", "PickerVenezuela", "PickerJapan",
        "PickerFrance", "PickerItaly", "PickerSpain");

    public IReadOnlyList<string> Options => Enumerable.Range(1, 4)
        .Select(index => Format("AutocompleteOptionFormat", index)).ToArray();

    public IList DisabledCountries => new AvaloniaList<object>
        { Text("PickerArgentina"), Text("PickerJapan"), Text("PickerFrance") };

    public IReadOnlyList<AlderPickerOption> DisabledAnimals =>
    [
        Option("PickerDog"), Option("PickerCat", enabled: false), Option("AutocompleteBird"),
        Option("PickerKangaroo", enabled: false), Option("PickerElephant"), Option("PickerTiger")
    ];

    public IReadOnlyList<AlderPickerOption> CountrySections =>
    [
        Option("AutocompleteUnitedStates", "AutocompleteNorthAmerica"), Option("AutocompleteCanada", "AutocompleteNorthAmerica"),
        Option("AutocompleteMexico", "AutocompleteNorthAmerica"), Option("AutocompleteUnitedKingdom", "AutocompleteEurope"),
        Option("PickerFrance", "AutocompleteEurope"), Option("AutocompleteGermany", "AutocompleteEurope"),
        Option("PickerSpain", "AutocompleteEurope"), Option("PickerItaly", "AutocompleteEurope"),
        Option("PickerJapan", "AutocompleteAsia"), Option("AutocompleteChina", "AutocompleteAsia"),
        Option("AutocompleteIndia", "AutocompleteAsia"), Option("AutocompleteSouthKorea", "AutocompleteAsia")
    ];

    public IReadOnlyList<AutocompleteCurrency> Currencies => CurrencyCodes
        .Select(currency => new AutocompleteCurrency(currency.Code, Text("Autocomplete" + currency.Code), currency.Symbol)).ToArray();

    public static Func<object?, string?> CurrencyText { get; } = item =>
        item is AutocompleteCurrency currency ? currency.Code : null;

    public static Func<string, object?, bool> CurrencyFilter { get; } = (query, item) =>
        item is AutocompleteCurrency currency && (Contains(currency.Code, query) || Contains(currency.Name, query));

    public IReadOnlyList<AlderPickerOption> Users => Enumerable.Range(1, 1000)
        .Select(index => new AlderPickerOption { Text = Format("AutocompleteUserFormat", index), Description = $"user{index}@example.com" })
        .ToArray();

    public static Func<string, object?, bool> UserFilter { get; } = (query, item) =>
        item is AlderPickerOption option && (Contains(option.Text, query) || Contains(option.Description ?? "", query));

    public IReadOnlyList<string> CharacterNames => Characters;
    public IList SelectedStates => selectedStates;
    public ICommand ToggleOpen { get; }
    public ICommand Submit { get; }
    public ICommand ResetSelectedStates { get; }
    public ICommand ResetTripStates { get; }
    public ICommand ResetMultipleStates { get; }
    public ICommand ResetVariants { get; }

    public AvaloniaList<object> TripStates { get; } = [];
    public AvaloniaList<object> MultipleStates { get; } = [];
    public AvaloniaList<object> PrimaryTags { get; } = [];
    public AvaloniaList<object> SecondaryTags { get; } = [];
    public int PrimarySelection { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = -1;
    public int SecondarySelection { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = -1;

    public string SelectedStateStatus => Format("AutocompleteSelectedFormat",
        Text(SelectedState as string ?? "FeedbackNone"));
    public string SelectedStatesStatus => Format("AutocompleteSelectedFormat", selectedStates.Count == 0
        ? Text("FeedbackNone") : string.Join(DisplayCulture.TextInfo.ListSeparator + " ", selectedStates.OfType<string>().Select(Text)));
    public string OpenStatus => Text(IsOpen ? "AutocompleteIsOpen" : "AutocompleteIsClosed");
    public string OpenButtonText => Text(IsOpen ? "AutocompleteClose" : "AutocompleteOpen");
    public bool IsStateInvalid => FormSubmitted && RequiredState is null;
    public bool IsCountryInvalid => FormSubmitted && RequiredCountry is null;

    public object? SelectedState
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(SelectedStateStatus));
        }
    }

    public object? RequiredState
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsStateInvalid));
        }
    }

    public object? RequiredCountry
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsCountryInvalid));
        }
    }

    private bool FormSubmitted
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsStateInvalid));
            this.RaisePropertyChanged(nameof(IsCountryInvalid));
        }
    }

    public bool IsOpen
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(OpenStatus));
            this.RaisePropertyChanged(nameof(OpenButtonText));
        }
    }

    public string? Query
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            _ = SearchAsync(value);
        }
    }

    public bool IsSearching
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public Func<string, object?, bool> CharacterFilter
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = static (_, _) => false;

    private async Task SearchAsync(string? query)
    {
        searchRequest?.Cancel();
        using var request = new CancellationTokenSource();
        searchRequest = request;
        IsSearching = !string.IsNullOrWhiteSpace(query);
        try
        {
            if (!IsSearching) return;
            await Task.Delay(260, request.Token);
            CharacterFilter = (input, item) => input == query && item is string name && Contains(name, input);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(searchRequest, request))
            {
                searchRequest = null;
                IsSearching = false;
            }
        }
    }

    private IReadOnlyList<string> Labels(params string[] keys) => keys.Select(Text).ToArray();

    private AlderPickerOption Option(string text, string? group = null, bool enabled = true) =>
        new() { Text = Text(text), Group = group is null ? null : Text(group), IsEnabled = enabled };

    private static bool Contains(string value, string query) => CultureInfo.CurrentCulture.CompareInfo.IndexOf(
        value, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}
