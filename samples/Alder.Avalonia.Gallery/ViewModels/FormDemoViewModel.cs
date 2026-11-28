using System.Collections;
using System.ComponentModel;
using System.Net.Mail;
using System.Windows.Input;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class FormDemoViewModel : GalleryViewModel, INotifyDataErrorInfo
{
    public string? ProjectName { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public string? Notes { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public bool IsSubmitting { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
    public bool Submitted { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
    public ICommand SaveCommand { get; }

    public string? Email
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Email)));
        }
    }

    public string? SampleEmail
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsSampleEmailInvalid));
        }
    } = "team@";

    public bool IsSampleEmailInvalid => IsInvalidEmail(SampleEmail);
    public bool HasErrors => IsInvalidEmail(Email);
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public FormDemoViewModel() => SaveCommand = ReactiveCommand.CreateFromTask(async () =>
    {
        IsSubmitting = true;
        Submitted = false;
        try
        {
            await Task.Delay(450);
            Submitted = true;
        }
        finally { IsSubmitting = false; }
    });

    public IEnumerable GetErrors(string? propertyName) =>
        (propertyName is null or nameof(Email)) && HasErrors ? new[] { Text("FormEmailInvalid") } : Array.Empty<string>();

    public void Reset()
    {
        ProjectName = null;
        Email = null;
        Notes = null;
        Submitted = false;
    }

    private static bool IsInvalidEmail(string? value) => !string.IsNullOrWhiteSpace(value) &&
        (!MailAddress.TryCreate(value, out var address) || address.Address != value);
}
