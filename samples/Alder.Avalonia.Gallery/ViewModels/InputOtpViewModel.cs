using System.Windows.Input;
using Alder.Avalonia.Gallery.Localization;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class InputOtpViewModel : GalleryViewModel
{
    public const string DemoCode = "123456";

    public string? ControlledCode
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Progress));
        }
    }

    public string Progress => Format("CodeDigits", ControlledCode?.Length ?? 0);

    public string? VerificationCode
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            IsInvalid = false;
            VerificationStatusMessage = "";
            this.RaisePropertyChanged(nameof(CanVerify));
        }
    }

    public bool IsInvalid
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsVerifying
    {
        get => field;
        private set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(VerifyLabel));
            this.RaisePropertyChanged(nameof(CanVerify));
        }
    }

    public bool CanVerify => IsVerifying || VerificationCode?.Length == DemoCode.Length;
    public string VerifyLabel => Text(IsVerifying ? "Verifying" : "VerifyCode");

    public string VerificationStatus => Display(VerificationStatusMessage);

    private GalleryMessage VerificationStatusMessage
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value, nameof(VerificationStatus));
    } = "";

    public ICommand NotifyComplete { get; }
    public Func<string, string> FormatPastedCode { get; } = value => value.Replace("-", "").Replace(" ", "");

    public InputOtpViewModel() => NotifyComplete = ReactiveCommand.Create<string>(code =>
    {
        if (code == VerificationCode) VerificationStatusMessage = "CodeCompleteReadyToVerify";
    });

    public void Reset() => ControlledCode = "";

    public async Task VerifyAsync()
    {
        if (IsVerifying || VerificationCode is not { Length: 6 } code) return;
        IsVerifying = true;
        VerificationStatusMessage = "";
        await Task.Delay(650);
        if (code == DemoCode)
        {
            VerificationCode = "";
            VerificationStatusMessage = "CodeVerified";
        }
        else
        {
            IsInvalid = true;
            VerificationStatusMessage = "InvalidCodePleaseTryAgain";
        }
        IsVerifying = false;
    }

    public void Verify() => _ = VerifyAsync();
}
