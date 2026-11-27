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

    public bool BlurSurface
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(SurfaceBackdropBlurRadius));
        }
    }

    public double SurfaceBackdropBlurRadius => BlurSurface ? 16 : 0;

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
