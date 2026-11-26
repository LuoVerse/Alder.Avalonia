using System.Windows.Input;
using Alder.Avalonia.Gallery.Localization;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class CheckBoxViewModel : GalleryViewModel
{
    public IReadOnlyList<string> Channels
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = ["mail", "inbox"];

    public bool EmailEnabled
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(EmailStatus));
        }
    } = true;

    public string EmailStatus => Format("StatusFormat", new GalleryMessage(EmailEnabled ? "Enabled" : "Disabled"));

    public bool EmailChannel
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(AllChannels));
        }
    } = true;

    public bool InAppChannel
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(AllChannels));
        }
    }

    public bool? AllChannels
    {
        get => EmailChannel == InAppChannel ? EmailChannel : null;
        set
        {
            if (value is not bool selected) return;
            using (DelayChangeNotifications())
            {
                EmailChannel = selected;
                InAppChannel = selected;
            }
        }
    }

    public bool TermsAccepted
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            IsTermsInvalid = false;
            Submitted = false;
        }
    }

    public bool IsTermsInvalid
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool Submitted
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ICommand Submit { get; }

    public CheckBoxViewModel()
    {
        Submit = ReactiveCommand.Create(() =>
        {
            IsTermsInvalid = !TermsAccepted;
            Submitted = TermsAccepted;
        });
    }
}
