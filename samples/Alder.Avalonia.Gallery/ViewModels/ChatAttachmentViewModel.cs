using System.Collections.ObjectModel;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class ChatAttachmentViewModel : GalleryViewModel
{
    private readonly ObservableCollection<ChatAttachmentFile> files = [];
    private int generation;

    public ChatAttachmentViewModel()
    {
        Files = new(files);
        files.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(HasFiles));
        Attach = ReactiveCommand.CreateFromTask(PickFilesAsync);
        DropFiles = ReactiveCommand.CreateFromTask<IReadOnlyList<IStorageFile>>(AddFilesAsync);
        Remove = ReactiveCommand.Create<ChatAttachmentFile>(file =>
        {
            if (files.Remove(file)) file.Preview?.Dispose();
        });
        Send = ReactiveCommand.Create<string?>(text =>
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Draft = string.Empty;
            ClearFiles();
        });
    }

    public string? Draft { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public ReadOnlyObservableCollection<ChatAttachmentFile> Files { get; }
    public bool HasFiles => files.Count > 0;
    public ICommand Attach { get; }
    public ICommand DropFiles { get; }
    public ICommand Remove { get; }
    public ICommand Send { get; }

    private async Task PickFilesAsync()
    {
        var owner = App.GalleryTopLevel;
        if (owner?.StorageProvider is not { CanOpen: true } storage) return;

        int session = generation;
        IReadOnlyList<IStorageFile> picked;
        try
        {
            picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Text("AiChooseAttachments"),
                AllowMultiple = true
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return;
        }

        if (session == generation)
            await AddFilesAsync(picked);
        else
            foreach (var file in picked) file.Dispose();
    }

    private async Task AddFilesAsync(IReadOnlyList<IStorageFile> picked)
    {
        int session = generation;
        try
        {
            foreach (var file in picked)
            {
                if (session != generation) return;
                var name = file.Name;
                ulong? size = null;
                Bitmap? preview = null;
                try
                {
                    size = (await file.GetBasicPropertiesAsync()).Size;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    // Files with unavailable metadata still have a usable attachment name.
                }

                if (session != generation) return;
                if (AlderChatAttachment.InferFileKind(null, name) == ChatAttachmentFileKind.Image)
                    preview = await LoadThumbnailAsync(file);

                if (session == generation)
                    files.Add(new(name, size, preview));
                else
                    preview?.Dispose();
            }
        }
        finally
        {
            foreach (var file in picked) file.Dispose();
        }
    }

    private static async Task<Bitmap?> LoadThumbnailAsync(IStorageFile file)
    {
        try
        {
            await using var stream = await file.OpenReadAsync();
            return await Task.Run(() => Bitmap.DecodeToWidth(stream, 128));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    internal void ClearFiles()
    {
        generation++;
        var removed = files.ToArray();
        files.Clear();
        foreach (var file in removed) file.Preview?.Dispose();
    }
}

public sealed class ChatAttachmentFile(string name, ulong? size, Bitmap? preview)
{
    public string Name { get; } = name;
    public ulong? Size { get; } = size;
    public Bitmap? Preview { get; } = preview;
}
