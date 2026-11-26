using System.Globalization;
using System.Net.Mail;
using Alder.Avalonia.Gallery.Localization;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class InputGroupViewModel : GalleryViewModel
{
    public string? Website
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "alder.example";

    public bool IsPasswordVisible
    {
        get => field;
        private set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(PasswordMask));
        }
    }

    public char PasswordMask => IsPasswordVisible ? '\0' : '•';

    public string? Email
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsEmailInvalid));
        }
    } = "jane@";

    public string? Price
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsPriceInvalid));
        }
    } = "0";

    public string? Feedback
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(FeedbackCount));
            this.RaisePropertyChanged(nameof(IsFeedbackInvalid));
        }
    }

    public string CopyStatus => Display(CopyStatusMessage);

    private GalleryMessage CopyStatusMessage
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value, nameof(CopyStatus));
    } = "";

    public string FeedbackStatus => Display(FeedbackStatusMessage);

    private GalleryMessage FeedbackStatusMessage
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value, nameof(FeedbackStatus));
    } = "";

    public bool IsEmailInvalid => !MailAddress.TryCreate(Email, out var address) || address.Address != Email;
    public bool IsPriceInvalid => !decimal.TryParse(Price, NumberStyles.Number, DisplayCulture, out var price) || price <= 0;
    public bool IsFeedbackInvalid => Feedback?.Length > 500;
    public string FeedbackCount => $"{Feedback?.Length ?? 0}/500";

    public void TogglePassword() => IsPasswordVisible = !IsPasswordVisible;

    public void SendFeedback()
    {
        if (string.IsNullOrWhiteSpace(Feedback) || IsFeedbackInvalid) return;
        Feedback = null;
        FeedbackStatusMessage = "FeedbackSubmitted";
    }

    public void ReportCopy(bool success) => CopyStatusMessage = success ? "WebsiteCopied" : "ClipboardIsUnavailable";
}
