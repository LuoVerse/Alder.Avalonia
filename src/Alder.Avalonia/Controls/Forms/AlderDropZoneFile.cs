using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

using Avalonia.Platform.Storage;

namespace Alder.Avalonia.Controls;

/// <summary>The state reported by the application for a selected file.</summary>
public enum DropZoneFileStatus
{
    /// <summary>The file is ready to be processed.</summary>
    Ready,
    /// <summary>The file is being processed.</summary>
    Uploading,
    /// <summary>The application finished processing the file.</summary>
    Complete,
    /// <summary>The application could not finish processing the file.</summary>
    Failed
}

/// <summary>A file selected through a drop zone and its optional application-reported progress.</summary>
public sealed partial class AlderDropZoneFile : AvaloniaObject
{
    internal AlderDropZoneFile(IStorageFile file, ulong? size) { File = file; Size = size; }

    /// <summary>The file handle passed to the application for reading or uploading.</summary>
    public IStorageFile File { get; }

    /// <summary>The file's display name.</summary>
    public string Name => File.Name;

    /// <summary>The size in bytes, when the storage provider supplies it.</summary>
    public ulong? Size { get; }

    /// <summary>The file extension in upper case for use in an icon badge.</summary>
    public string Format
    {
        get
        {
            var extension = Path.GetExtension(Name).TrimStart('.');
            return extension[..Math.Min(extension.Length, 5)].ToUpperInvariant();
        }
    }

    /// <summary>The size formatted using the current culture and decimal units.</summary>
    public string SizeText
    {
        get
        {
            if (Size is not { } size) return string.Empty;
            var (value, unit) = size switch
            {
                >= 1_000_000_000 => (size / 1_000_000_000d, "GB"),
                >= 1_000_000 => (size / 1_000_000d, "MB"),
                >= 1_000 => (size / 1_000d, "KB"),
                _ => ((double)size, "B")
            };
            return string.Create(CultureInfo.CurrentCulture, $"{value:0.#} {unit}");
        }
    }

    /// <summary>The processing state shown in the file list.</summary>
    [GeneratedStyledProperty]
    public partial DropZoneFileStatus Status { get; set; }

    /// <summary>The progress percentage shown while processing a file.</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<AlderDropZoneFile, double>(nameof(Progress),
            coerce: (_, value) => double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0);

    /// <summary>The progress percentage shown while processing a file, from 0 to 100.</summary>
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
}

/// <summary>Describes why a file could not be added to the drop zone.</summary>
public enum DropZoneFileError
{
    /// <summary>No error occurred.</summary>
    None,
    /// <summary>The item is a directory or does not match an allowed file name pattern.</summary>
    Type,
    /// <summary>The file exceeds the configured size limit.</summary>
    Size,
    /// <summary>The drop zone cannot accept additional files.</summary>
    Count,
    /// <summary>The storage provider could not read the file metadata or open the picker.</summary>
    Unavailable
}

/// <summary>A rejected storage item and its validation error.</summary>
/// <param name="Item">The storage item, when one was available.</param>
/// <param name="Error">The reason it was rejected.</param>
public sealed record DropZoneRejectedFile(IStorageItem? Item, DropZoneFileError Error);

/// <summary>The files accepted and rejected during a single selection or drop.</summary>
public sealed class DropZoneFilesChangedEventArgs(
    IReadOnlyList<AlderDropZoneFile> added, IReadOnlyList<DropZoneRejectedFile> rejected) : EventArgs
{
    /// <summary>Files added in this operation.</summary>
    public IReadOnlyList<AlderDropZoneFile> Added { get; } = added;

    /// <summary>Items rejected in this operation.</summary>
    public IReadOnlyList<DropZoneRejectedFile> Rejected { get; } = rejected;
}

internal static class DropZoneStatus
{
    public static FuncValueConverter<DropZoneFileStatus, bool> Processing { get; } =
        new(status => status != DropZoneFileStatus.Ready);
    public static FuncValueConverter<DropZoneFileStatus, bool> Uploading { get; } =
        new(status => status == DropZoneFileStatus.Uploading);
    public static FuncValueConverter<DropZoneFileStatus, bool> Complete { get; } =
        new(status => status == DropZoneFileStatus.Complete);
    public static FuncValueConverter<DropZoneFileStatus, bool> Failed { get; } =
        new(status => status == DropZoneFileStatus.Failed);
}
