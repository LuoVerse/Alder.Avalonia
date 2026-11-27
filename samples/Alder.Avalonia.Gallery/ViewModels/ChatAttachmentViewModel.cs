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
                var attachment = await ChatAttachmentFile.ReadAsync(file);
                if (session == generation)
                    files.Add(attachment);
                else
                    attachment.Preview?.Dispose();
            }
        }
        finally
        {
            foreach (var file in picked) file.Dispose();
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

    internal static async Task<ChatAttachmentFile> ReadAsync(IStorageFile file)
    {
        var name = file.Name;
        ulong? size = null;
        try
        {
            size = (await file.GetBasicPropertiesAsync()).Size;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Files with unavailable metadata still have a usable attachment name.
        }

        var preview = AlderChatAttachment.InferFileKind(null, name) == ChatAttachmentFileKind.Image
            ? await LoadPreviewAsync(file) : null;
        return new ChatAttachmentFile(name, size, preview);
    }

    private static async Task<Bitmap?> LoadPreviewAsync(IStorageFile file)
    {
        try
        {
            await using var stream = await file.OpenReadAsync();
            return await Task.Run(() => new Bitmap(stream));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

}
