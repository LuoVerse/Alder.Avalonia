using System.Windows.Input;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class SalashWindowViewModel : GalleryViewModel, IDialogContext
{
    public string Account { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = "";
    public string Password { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = "";
    public bool IsPasswordVisible { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public bool IsSigningIn { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
    public string? Error { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
    public ICommand SignIn { get; }
    public ICommand TogglePassword { get; }
    public event EventHandler<DialogResult>? RequestClose;

    public SalashWindowViewModel()
    {
        SignIn = ReactiveCommand.CreateFromTask(SignInAsync);
        TogglePassword = ReactiveCommand.Create(() => IsPasswordVisible = !IsPasswordVisible);
    }

    private async Task SignInAsync()
    {
        if (string.IsNullOrWhiteSpace(Account) || string.IsNullOrEmpty(Password))
        {
            Error = Text("SalashRequired");
            return;
        }

        Error = null;
        IsSigningIn = true;
        try
        {
            // Gallery simulates an asynchronous authentication service.
            await Task.Delay(300);
        }
        finally { IsSigningIn = false; }

        RequestClose?.Invoke(this, DialogResult.OK);
    }
}
