using System.Windows.Input;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class SalashWindowViewModel : GalleryViewModel, IDialogContext
{
    public string Account { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = "";
    public string Password { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = "";
    public bool IsPasswordVisible { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public string? Error { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
    public ICommand SignIn { get; }
    public ICommand TogglePassword { get; }
    public event EventHandler<DialogResult>? RequestClose;

    public SalashWindowViewModel()
    {
        SignIn = ReactiveCommand.Create(CompleteSignIn);
        TogglePassword = ReactiveCommand.Create(() => IsPasswordVisible = !IsPasswordVisible);
    }

    private void CompleteSignIn()
    {
        if (string.IsNullOrWhiteSpace(Account) || string.IsNullOrEmpty(Password))
        {
            Error = Text("SalashRequired");
            return;
        }

        Error = null;
        RequestClose?.Invoke(this, DialogResult.OK);
    }
}
