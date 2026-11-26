using System.Net.Mail;
using System.Windows.Input;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class InputViewModel : ReactiveObject
{
    private const string DefaultDomain = "alder.example";

    public string? Domain
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(DomainPreview));
        }
    } = DefaultDomain;

    public string DomainPreview => $"https://{(string.IsNullOrWhiteSpace(Domain) ? "your-domain" : Domain)}";

    public string? ContactEmail
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsContactEmailInvalid));
        }
    } = "jane@";

    public bool IsContactEmailInvalid =>
        !MailAddress.TryCreate(ContactEmail, out var address) || address.Address != ContactEmail;

    public ICommand Reset { get; }

    public InputViewModel() => Reset = ReactiveCommand.Create(() => { Domain = DefaultDomain; });
}
