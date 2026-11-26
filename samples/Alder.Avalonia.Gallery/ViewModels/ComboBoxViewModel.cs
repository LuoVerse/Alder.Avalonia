using System.Globalization;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class ComboBoxViewModel : GalleryViewModel
{
    private const int PageSize = 10;
    private static readonly string[] AnimalKeys = ["aardvark", "cat", "dog", "kangaroo", "panda", "snake"];
    private static readonly string[] PetKeys = ["cat", "dog", "bird", "fish", "hamster"];
    private static readonly Dictionary<string, string> AnimalLabels = new(StringComparer.Ordinal)
    {
        ["aardvark"] = "PickerAardvark", ["cat"] = "PickerCat", ["dog"] = "PickerDog",
        ["kangaroo"] = "PickerKangaroo", ["panda"] = "PickerPanda", ["snake"] = "PickerSnake",
        ["bird"] = "AutocompleteBird", ["fish"] = "ComboFish", ["hamster"] = "ComboHamster"
    };

    public IReadOnlyList<string> Characters { get; } =
    [
        "Luke Skywalker", "C-3PO", "R2-D2", "Darth Vader", "Leia Organa", "Owen Lars", "Beru Whitesun lars",
        "R5-D4", "Biggs Darklighter", "Obi-Wan Kenobi", "Anakin Skywalker", "Wilhuff Tarkin", "Chewbacca",
        "Han Solo", "Greedo", "Jabba Desilijic Tiure", "Wedge Antilles", "Jek Tono Porkins", "Yoda",
        "Palpatine", "Boba Fett", "IG-88", "Bossk", "Lando Calrissian", "Lobot", "Ackbar", "Mon Mothma",
        "Arvel Crynyd", "Wicket Systri Warrick", "Nien Nunb"
    ];

    private CancellationTokenSource? searchRequest;
    private int loadedCount = PageSize;
    private bool hasNextPage = true;

    public ComboBoxViewModel()
    {
        AsyncFilter = MakeFilter("", PageSize);
        LoadMore = ReactiveCommand.CreateFromTask(async () =>
        {
            if (hasNextPage)
                await SearchAsync(AsyncQuery?.Trim() ?? "", loadedCount + PageSize);
        });
        SubmitValues = ReactiveCommand.Create(() =>
        {
            FormSubmitted = true;
            this.RaisePropertyChanged(nameof(FormKeyInvalid));
            this.RaisePropertyChanged(nameof(FormTextInvalid));
            this.RaisePropertyChanged(nameof(FormStatus));
        });
    }

    public IReadOnlyList<string> Animals => AnimalKeys;
    public IReadOnlyList<string> Pets => PetKeys;
    public IReadOnlyList<string> DisplayedAnimals => AnimalKeys.Select(AnimalName).ToArray();
    public IReadOnlyList<string> Frameworks { get; } = ["Avalonia", "WPF", ".NET MAUI"];
    public IReadOnlyList<SelectExampleUser> Users { get; } = SelectViewModel.CreateUsers();

    public Func<object?, string?> Caption
    {
        get
        {
            var labels = AnimalLabels.ToDictionary(pair => pair.Key, pair => Text(pair.Value), StringComparer.Ordinal);
            return item => item is string key ? labels.GetValueOrDefault(key) : null;
        }
    }

    public Func<string, object?, bool> PrefixFilter
    {
        get
        {
            var caption = Caption;
            return (query, item) => caption(item) is { } name &&
                DisplayCulture.CompareInfo.IsPrefix(name, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
        }
    }

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

    public ComboBoxFormViewModel Required { get; } = new();
    public ComboBoxFormViewModel Surface { get; } = new();
    public ComboBoxFormViewModel NativeValidation { get; } = new();
    public ComboBoxFormViewModel LiveValidation { get; } = new(live: true);
    public ComboBoxFormViewModel CustomValidation { get; } = new(rejectSnake: true);
    public ICommand LoadMore { get; }
    public ICommand SubmitValues { get; }
    public string SelectedStatus => Format("AutocompleteSelectedFormat", AnimalName(SelectedAnimal));
    public string InputStatus => Format("ComboInputStatus", string.IsNullOrEmpty(ControlledText) ? Text("ComboEmptyInput") : ControlledText);
    public bool FormKeyInvalid => FormSubmitted && FormKey is null;
    public bool FormTextInvalid => FormSubmitted && FormText is null;
    public string? FormStatus => FormSubmitted && FormKey is string key && FormText is string value
        ? Format("ComboFormStatus", key, AnimalName(value)) : null;

    private bool FormSubmitted { get; set; }

    public object? SelectedAnimal
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(SelectedStatus));
        }
    } = "cat";

    public string? ControlledText
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(InputStatus));
        }
    }

    public object? FormKey
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(FormKeyInvalid));
        }
    }

    public object? FormText
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(FormTextInvalid));
        }
    }

    public string? AsyncQuery
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            _ = SearchAsync(value?.Trim() ?? "", PageSize);
        }
    }

    public bool IsSearching
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public Func<string, object?, bool> AsyncFilter
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    private async Task SearchAsync(string query, int count)
    {
        searchRequest?.Cancel();
        using var request = new CancellationTokenSource();
        searchRequest = request;
        IsSearching = true;
        try
        {
            await Task.Delay(240, request.Token);
            loadedCount = count;
            AsyncFilter = MakeFilter(query, count);
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

    private Func<string, object?, bool> MakeFilter(string query, int count)
    {
        var matches = Characters.Where(value => DisplayCulture.CompareInfo.IndexOf(value, query,
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0).ToArray();
        hasNextPage = matches.Length > count;
        var visible = matches.Take(count).ToHashSet();
        var firstPage = Characters.Take(PageSize).ToHashSet();
        return (input, item) => item is string name &&
            (input == query ? visible.Contains(name) : input.Length == 0 && firstPage.Contains(name));
    }

    private string AnimalName(object? value) =>
        value is string key && AnimalLabels.TryGetValue(key, out var label) ? Text(label) : Text("FeedbackNone");

    private AlderPickerOption Option(string key, string? group = null, bool enabled = true) =>
        new() { Text = Text(key), Group = group is null ? null : Text(group), IsEnabled = enabled };
}

public sealed class ComboBoxFormViewModel : GalleryViewModel
{
    private readonly bool live;
    private readonly bool rejectSnake;
    private bool submitted;
    private bool touched;

    public ComboBoxFormViewModel(bool live = false, bool rejectSnake = false)
    {
        this.live = live;
        this.rejectSnake = rejectSnake;
        Submit = ReactiveCommand.Create(() =>
        {
            submitted = true;
            UpdateValidation();
        });
    }

    public ICommand Submit { get; }
    public bool IsInvalid => ErrorText is not null;

    public string? ErrorText => submitted || (live && touched) ? Validate() : null;
    public string? Status => submitted && (live || !IsInvalid)
        ? Text(IsInvalid ? "ComboSubmittedWithErrors" : "ComboSubmitted") : null;

    public object? Selected
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            UpdateValidation();
        }
    }

    public string? Input
    {
        get => field;
        set
        {
            if (field == value) return;
            touched = true;
            this.RaiseAndSetIfChanged(ref field, value);
            UpdateValidation();
        }
    }

    private string? Validate() => Selected switch
    {
        null => Text("ComboRequiredError"),
        "snake" when rejectSnake => Text("ComboSnakeError"),
        _ => null
    };

    private void UpdateValidation()
    {
        this.RaisePropertyChanged(nameof(ErrorText));
        this.RaisePropertyChanged(nameof(IsInvalid));
        this.RaisePropertyChanged(nameof(Status));
    }
}
