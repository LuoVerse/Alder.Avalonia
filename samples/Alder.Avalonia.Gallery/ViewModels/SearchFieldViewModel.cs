using System.Windows.Input;
using Alder.Avalonia.Gallery.Localization;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class SearchFieldViewModel : GalleryViewModel
{
    private const int MinimumFormQueryLength = 3;

    public string? Query
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsQueryInvalid));
            StatusMessage = "";
        }
    }

    public string Status => Display(StatusMessage);

    private GalleryMessage StatusMessage
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value, nameof(Status));
    } = "";

    public bool IsQueryInvalid => Query is { Length: > 0 } query && query.Trim().Length < 3;
    public string ShortcutLabel => OperatingSystem.IsMacOS() ? "⌘ K" : "Ctrl K";
    public ICommand Submit { get; }

    public string FormQuery
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value ?? "");
            this.RaisePropertyChanged(nameof(IsFormInvalid));
        }
    } = "";

    public bool IsFormInvalid => FormQuery.Length is > 0 and < MinimumFormQueryLength;

    public bool IsFormSubmitting
    {
        get => field;
        private set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(FormButtonLabel));
        }
    }

    public string FormButtonLabel => Text(IsFormSubmitting ? "SearchFormPending" : "Search");
    public ICommand SubmitForm { get; }

    public SearchFieldViewModel()
    {
        Submit = ReactiveCommand.Create<string>(query =>
        {
            StatusMessage = string.IsNullOrWhiteSpace(query) ? "EnterASearchQuery" : new GalleryMessage("SearchedFor", query.Trim());
        });

        var canSubmit = this.WhenAnyValue(model => model.FormQuery, query => query.Length >= MinimumFormQueryLength);
        SubmitForm = ReactiveCommand.CreateFromTask<string>(SubmitFormAsync, canSubmit);
    }

    private async Task SubmitFormAsync(string query)
    {
        if (query.Length < MinimumFormQueryLength || IsFormSubmitting) return;
        IsFormSubmitting = true;
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1500));
            if (FormQuery == query) FormQuery = "";
        }
        finally
        {
            IsFormSubmitting = false;
        }
    }
}
