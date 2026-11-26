using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Globalization;
using System.Windows.Input;

namespace Alder.Avalonia.Controls;

/// <summary>An attachment with optional preview, metadata and independent open and remove actions.</summary>
public sealed partial class AlderChatAttachment : ContentControl
{
    /// <summary>The file name shown to the reader.</summary>
    [GeneratedStyledProperty]
    public partial string? FileName { get; set; }

    /// <summary>Optional metadata. Overrides the formatted file size when set.</summary>
    [GeneratedStyledProperty]
    public partial string? Description { get; set; }

    /// <summary>File size in bytes, if available.</summary>
    [GeneratedStyledProperty]
    public partial ulong? FileSize { get; set; }

    /// <summary>Optional MIME type used to choose the file icon.</summary>
    [GeneratedStyledProperty]
    public partial string? MimeType { get; set; }

    /// <summary>Optional thumbnail supplied by the host. Content can supply a custom media preview.</summary>
    [GeneratedStyledProperty]
    public partial IImage? Preview { get; set; }

    /// <summary>Opens the represented attachment.</summary>
    [GeneratedStyledProperty]
    public partial ICommand? OpenCommand { get; set; }

    /// <summary>Removes the represented attachment.</summary>
    [GeneratedStyledProperty]
    public partial ICommand? RemoveCommand { get; set; }

    /// <summary>The payload supplied to both commands.</summary>
    [GeneratedStyledProperty]
    public partial object? CommandParameter { get; set; }

    /// <summary>Identifies the inferred file kind.</summary>
    public static readonly DirectProperty<AlderChatAttachment, ChatAttachmentFileKind> FileKindProperty =
        AvaloniaProperty.RegisterDirect<AlderChatAttachment, ChatAttachmentFileKind>(nameof(FileKind), control => control.FileKind);

    /// <summary>File category resolved from the MIME type and name.</summary>
    public ChatAttachmentFileKind FileKind => fileKind;
    private ChatAttachmentFileKind fileKind;

    /// <summary>Identifies the metadata shown by the default template.</summary>
    public static readonly DirectProperty<AlderChatAttachment, string?> DisplayDescriptionProperty =
        AvaloniaProperty.RegisterDirect<AlderChatAttachment, string?>(nameof(DisplayDescription), control => control.DisplayDescription);

    /// <summary>The description override or formatted size.</summary>
    public string? DisplayDescription => displayDescription;
    private string? displayDescription;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FileNameProperty || change.Property == MimeTypeProperty)
            SetAndRaise(FileKindProperty, ref fileKind, InferFileKind(MimeType, FileName));
        if (change.Property == DescriptionProperty || change.Property == FileSizeProperty)
            SetAndRaise(DisplayDescriptionProperty, ref displayDescription, Description ?? FormatSize(FileSize));
        if (change.Property == PreviewProperty || change.Property == ContentProperty)
            PseudoClasses.Set(":media", Preview is not null || Content is not null);
        if (change.Property == OpenCommandProperty)
            PseudoClasses.Set(":openable", OpenCommand is not null);
    }

    /// <summary>Resolves a file icon category from a MIME type or file extension.</summary>
    public static ChatAttachmentFileKind InferFileKind(string? mimeType, string? fileName)
    {
        var media = mimeType?.Split(';', 2)[0].Trim().ToLowerInvariant();
        var byMime = media switch
        {
            "application/pdf" => ChatAttachmentFileKind.Pdf,
            "application/zip" or "application/gzip" or "application/x-7z-compressed" or
                "application/vnd.rar" or "application/x-rar-compressed" or "application/x-tar" => ChatAttachmentFileKind.Archive,
            "text/csv" or "text/tab-separated-values" or "application/vnd.ms-excel" or
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or
                "application/vnd.oasis.opendocument.spreadsheet" => ChatAttachmentFileKind.Spreadsheet,
            "application/msword" or "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or
                "application/vnd.oasis.opendocument.text" or "application/rtf" => ChatAttachmentFileKind.Document,
            "application/json" or "application/xml" or "text/xml" or "text/css" or "text/html" or
                "text/javascript" or "application/javascript" or "application/typescript" => ChatAttachmentFileKind.Code,
            _ when media?.StartsWith("image/", StringComparison.Ordinal) == true => ChatAttachmentFileKind.Image,
            _ when media?.StartsWith("video/", StringComparison.Ordinal) == true => ChatAttachmentFileKind.Video,
            _ when media?.StartsWith("audio/", StringComparison.Ordinal) == true => ChatAttachmentFileKind.Audio,
            _ => ChatAttachmentFileKind.Unknown,
        };
        if (byMime != ChatAttachmentFileKind.Unknown) return byMime;

        return Path.GetExtension(fileName)?.ToLowerInvariant() switch
        {
            ".pdf" => ChatAttachmentFileKind.Pdf,
            ".zip" or ".rar" or ".tar" or ".gz" or ".7z" or ".bz2" or ".xz" => ChatAttachmentFileKind.Archive,
            ".xls" or ".xlsx" or ".xlsm" or ".csv" or ".tsv" or ".ods" => ChatAttachmentFileKind.Spreadsheet,
            ".doc" or ".docx" or ".txt" or ".rtf" or ".odt" or ".ppt" or ".pptx" => ChatAttachmentFileKind.Document,
            ".cs" or ".js" or ".jsx" or ".ts" or ".tsx" or ".json" or ".xml" or ".xaml" or ".axaml" or
                ".html" or ".css" or ".py" or ".rs" or ".go" or ".java" or ".cpp" or ".sh" or
                ".sql" or ".md" or ".yaml" or ".yml" => ChatAttachmentFileKind.Code,
            ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".bmp" or ".ico" or
                ".avif" or ".heic" or ".tif" or ".tiff" or ".svg" => ChatAttachmentFileKind.Image,
            ".mp4" or ".mov" or ".mkv" or ".avi" or ".webm" or ".m4v" => ChatAttachmentFileKind.Video,
            ".mp3" or ".m4a" or ".wav" or ".aac" or ".ogg" or ".flac" => ChatAttachmentFileKind.Audio,
            _ when media == "text/plain" => ChatAttachmentFileKind.Document,
            _ => ChatAttachmentFileKind.Unknown,
        };
    }

    private static string? FormatSize(ulong? bytes) => bytes switch
    {
        null => null,
        < 1024 => string.Create(CultureInfo.CurrentCulture, $"{bytes} B"),
        < 1024 * 1024 => string.Create(CultureInfo.CurrentCulture, $"{bytes / 1024d:0} KB"),
        < 1024 * 1024 * 1024 => string.Create(CultureInfo.CurrentCulture, $"{bytes / (1024d * 1024):0.#} MB"),
        _ => string.Create(CultureInfo.CurrentCulture, $"{bytes / (1024d * 1024 * 1024):0.#} GB"),
    };
}

/// <summary>The file categories represented by Chat Attachment icons.</summary>
public enum ChatAttachmentFileKind
{
    /// <summary>A file without a recognized format.</summary>
    Unknown,
    /// <summary>A PDF document.</summary>
    Pdf,
    /// <summary>A compressed file or archive.</summary>
    Archive,
    /// <summary>A spreadsheet or tabular data file.</summary>
    Spreadsheet,
    /// <summary>A text document or presentation.</summary>
    Document,
    /// <summary>A source code or markup file.</summary>
    Code,
    /// <summary>An image file.</summary>
    Image,
    /// <summary>A video file.</summary>
    Video,
    /// <summary>An audio file.</summary>
    Audio
}
