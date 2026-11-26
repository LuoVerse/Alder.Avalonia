using Alder.Avalonia.Controls;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class DropZoneViewModel : GalleryViewModel
{
    public IReadOnlyList<FilePickerFileType> Assets => Filenames("FormAssets", ["*.jpeg", "*.jpg", "*.png", "*.pdf", "*.mp4"],
        ["image/jpeg", "image/png", "application/pdf", "video/mp4"]);
    public IReadOnlyList<FilePickerFileType> Images => Filenames("FormImages", ["*.jpeg", "*.jpg", "*.png", "*.gif", "*.webp", "*.svg"],
        ["image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml"]);
    public IReadOnlyList<FilePickerFileType> Photos => Filenames("FormImages", ["*.jpeg", "*.jpg", "*.png"], ["image/jpeg", "image/png"]);
    public IReadOnlyList<FilePickerFileType> Documents => Filenames("FormDocuments", ["*.pdf", "*.docx", "*.txt"],
        ["application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "text/plain"]);

    public async Task LoadExampleFilesAsync(AlderDropZone zone)
    {
        if (TopLevel.GetTopLevel(zone)?.StorageProvider is not { } storage) return;
        (string Key, long Bytes)[] samples = zone.IsFileListCompact
            ? [("FormDropLogoFile", 24_000), ("FormDropNotesFile", 350_000), ("FormDropRecordingFile", 5_000_000)]
            : [("FormDropReportFile", 2_200_000), ("FormDropBannerFile", 480_000), ("FormDropRecordingFile", 5_000_000)];
        DirectoryInfo folder = Directory.CreateTempSubdirectory("alder-drop-zone-");
        var handles = new List<IStorageFile>(samples.Length);
        try
        {
            foreach (var (key, bytes) in samples)
            {
                string path = Path.Combine(folder.FullName, Text(key));
                using (var stream = File.Create(path)) stream.SetLength(bytes);
                if (await storage.TryGetFileFromPathAsync(path) is { } file) handles.Add(file);
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

    public async Task AnimateFilesAsync(AlderDropZone zone, IReadOnlyList<AlderDropZoneFile> files)
    {
        var jobs = files.Select(async (file, index) =>
        {
            file.Status = DropZoneFileStatus.Uploading;
            for (int progress = 12; progress <= 100 && zone.Files.Contains(file); progress += 11)
            {
                file.Progress = progress;
                await Task.Delay(150);
            }
            if (!zone.Files.Contains(file)) return;
            file.Status = index == 2 ? DropZoneFileStatus.Failed : DropZoneFileStatus.Complete;
        });
        await Task.WhenAll(jobs);
    }

    private FilePickerFileType[] Filenames(string label, string[] patterns, string[] media) =>
        [new(Text(label)) { Patterns = patterns, MimeTypes = media }];

}
