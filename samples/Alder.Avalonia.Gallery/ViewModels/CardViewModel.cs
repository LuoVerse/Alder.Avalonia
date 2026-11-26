using System.Net.Mail;
using System.Windows.Input;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class CardViewModel : GalleryViewModel
{
    public CardViewModel() => SignIn = ReactiveCommand.Create(() =>
    {
        ResultKey = string.IsNullOrWhiteSpace(Email) || !MailAddress.TryCreate(Email, out var address) || address.Address != Email
            ? "CardInvalidEmail"
            : string.IsNullOrEmpty(Password) ? "CardMissingPassword" : "CardSignedIn";
    });

    public string? Email
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? Password
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ICommand SignIn { get; }

    public string Result => ResultKey is { } key ? Text(key) : string.Empty;

    private string? ResultKey
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Result));
        }
    }
}
