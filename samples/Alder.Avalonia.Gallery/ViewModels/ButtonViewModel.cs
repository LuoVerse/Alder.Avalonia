using System.Windows.Input;
using Alder.Avalonia.Gallery.Localization;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class ButtonViewModel : GalleryViewModel
{
    public bool IsUploading
    {
        get => field;
        private set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(UploadText));
        }
    }

    public string UploadText => Text(IsUploading ? "Uploading" : "UploadFile");

    public string Status => Display(StatusMessage);

    private GalleryMessage StatusMessage
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value, nameof(Status));
    } = "ClickUploadFileToSeeTheLoadingState";

    public ICommand Upload { get; }
    public ICommand Demonstrate { get; }

    public ButtonViewModel()
    {
        Upload = ReactiveCommand.Create(StartUpload);
        Demonstrate = ReactiveCommand.Create<string?>(message => StatusMessage = message ?? "");
    }

    // Keep the native button enabled so focus stays on it during the pending state.
    private void StartUpload() => _ = UploadAsync();

    private async Task UploadAsync()
    {
        if (IsUploading) return;
        IsUploading = true;
        StatusMessage = "UploadingFile";
        await Task.Delay(2000);
        IsUploading = false;
        StatusMessage = "UploadComplete";
    }
}
