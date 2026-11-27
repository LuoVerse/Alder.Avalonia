using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class DropZoneViewModel : GalleryViewModel
{
    private readonly Dictionary<AlderDropZoneFile, CancellationTokenSource> uploads = [];
    private readonly HashSet<IStorageFile> sampleFilesBeingAdded = new(ReferenceEqualityComparer.Instance);

    public DropZoneViewModel()
    {
        UploadCommand = ReactiveCommand.Create<DropZoneFilesChangedEventArgs>(QueueFiles);
        RetryCommand = ReactiveCommand.Create<AlderDropZoneFile>(file => StartUpload(file, simulateFailure: false));
        CancelCommand = ReactiveCommand.Create<AlderDropZoneFile>(CancelUpload);
    }

    public ICommand UploadCommand { get; }
    public ICommand RetryCommand { get; }
    public ICommand CancelCommand { get; }

    public IReadOnlyList<FilePickerFileType> Assets => Filenames("FormAssets", ["*.jpeg", "*.jpg", "*.png", "*.pdf", "*.mp4"],
        ["image/jpeg", "image/png", "application/pdf", "video/mp4"]);
    public IReadOnlyList<FilePickerFileType> Images => Filenames("FormImages", ["*.jpeg", "*.jpg", "*.png", "*.gif", "*.webp", "*.svg"],
        ["image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml"]);
    public IReadOnlyList<FilePickerFileType> Photos => Filenames("FormImages", ["*.jpeg", "*.jpg", "*.png"], ["image/jpeg", "image/png"]);
    public IReadOnlyList<FilePickerFileType> Documents => Filenames("FormDocuments", ["*.pdf", "*.docx", "*.txt"],
        ["application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "text/plain"]);

    // Only the Gallery's initial preview states require access to the control.
    public void InitializePreview(AlderDropZone zone) => _ = LoadExampleFilesAsync(zone);

    private async Task LoadExampleFilesAsync(AlderDropZone zone)
    {
        if (TopLevel.GetTopLevel(zone)?.StorageProvider is not { } storage) return;
        (string Key, long Bytes)[] samples = zone.IsFileListCompact
            ? [("FormDropLogoFile", 24_000), ("FormDropNotesFile", 350_000), ("FormDropRecordingFile", 5_000_000)]
            : [("FormDropReportFile", 2_200_000), ("FormDropBannerFile", 480_000), ("FormDropRecordingFile", 5_000_000)];
        var handles = new List<IStorageFile>(samples.Length);
        try
        {
            var folder = Directory.CreateTempSubdirectory("alder-drop-zone-");
            try
            {
                foreach (var (key, bytes) in samples)
                {
                    string path = Path.Combine(folder.FullName, Text(key));
                    using (var stream = File.Create(path)) stream.SetLength(bytes);
                    if (await storage.TryGetFileFromPathAsync(path) is { } file)
                    {
                        handles.Add(file);
                        sampleFilesBeingAdded.Add(file);
                    }
                }
                await zone.AddFilesAsync(handles);
                foreach (var file in zone.Files)
                {
                    int index = handles.IndexOf(file.File);
                    if (index < 0) continue;
                    file.Status = index switch { 0 => DropZoneFileStatus.Complete, 1 => DropZoneFileStatus.Uploading, _ => DropZoneFileStatus.Failed };
                    file.Progress = index switch { 0 => 100, 1 => zone.IsFileListCompact ? 68 : 42, _ => 0 };
                }
            }
            finally { folder.Delete(recursive: true); }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // The interactive picker still works if the Gallery cannot prepare its sample files.
        }
        finally
        {
            foreach (var handle in handles) sampleFilesBeingAdded.Remove(handle);
        }
    }

    private void QueueFiles(DropZoneFilesChangedEventArgs args)
    {
        for (int index = 0; index < args.Added.Count; index++)
        {
            var file = args.Added[index];
            if (!sampleFilesBeingAdded.Contains(file.File))
                StartUpload(file, simulateFailure: index == 2);
        }
    }

    private void StartUpload(AlderDropZoneFile file, bool simulateFailure)
    {
        if (uploads.ContainsKey(file)) return;
        var cancellation = new CancellationTokenSource();
        uploads.Add(file, cancellation);
        _ = SimulateUploadAsync(file, simulateFailure, cancellation);
    }

    private void CancelUpload(AlderDropZoneFile file)
    {
        if (uploads.TryGetValue(file, out var active))
            active.Cancel();
    }

    private async Task SimulateUploadAsync(AlderDropZoneFile file, bool simulateFailure, CancellationTokenSource cancellation)
    {
        try
        {
            file.Progress = 0;
            file.Status = DropZoneFileStatus.Uploading;
            for (int progress = 12; progress <= 100; progress += 11)
            {
                await Task.Delay(150, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                file.Progress = progress;
            }
            file.Status = simulateFailure ? DropZoneFileStatus.Failed : DropZoneFileStatus.Complete;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            uploads.Remove(file);
            cancellation.Dispose();
        }
    }

    private FilePickerFileType[] Filenames(string label, string[] patterns, string[] media) =>
        [new(Text(label)) { Patterns = patterns, MimeTypes = media }];
}
