using System.Collections.ObjectModel;
using System.IO.Enumeration;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Accepts files from the platform file picker or a drop, with shared validation and optional file rows.</summary>
public sealed partial class AlderDropZone : TemplatedControl
{
    /// <summary>The title shown in the drop area.</summary>
    [GeneratedStyledProperty]
    public partial string? Title { get; set; }

    /// <summary>The supporting instructions shown below the title.</summary>
    [GeneratedStyledProperty]
    public partial string? Description { get; set; }

    /// <summary>Optional artwork or icon above the title.</summary>
    [GeneratedStyledProperty]
    public partial object? Icon { get; set; }

    /// <summary>Optional content of the button that opens the file picker.</summary>
    [GeneratedStyledProperty]
    public partial object? BrowseContent { get; set; }

    /// <summary>Shows the standard browse button. Custom actions can use BrowseCommand.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool ShowBrowseButton { get; set; }

    /// <summary>Shows the drop surface. Hide it to display and manage the file list by itself.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool ShowDropArea { get; set; }

    /// <summary>Optional controls displayed after the standard browse button.</summary>
    [GeneratedStyledProperty]
    public partial object? Actions { get; set; }

    /// <summary>Allows multiple files to be added. Single-file mode replaces the previous file on success.</summary>
    [GeneratedStyledProperty]
    public partial bool AllowMultiple { get; set; }

    /// <summary>File-picker filters. Each filter's file name patterns also validate dropped files.</summary>
    [GeneratedStyledProperty]
    public partial IReadOnlyList<FilePickerFileType>? FileTypeFilter { get; set; }

    /// <summary>The maximum individual file size in bytes. Null leaves the size unrestricted.</summary>
    [GeneratedStyledProperty]
    public partial ulong? MaxFileSize { get; set; }

    /// <summary>The maximum number of selected files. Zero leaves the number unrestricted in multiple-file mode.</summary>
    [GeneratedStyledProperty]
    public partial int MaxFiles { get; set; }

    /// <summary>Displays the selected files below the drop area.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool ShowFileList { get; set; }

    /// <summary>Uses the more compact spacing for file rows.</summary>
    [GeneratedStyledProperty]
    public partial bool IsFileListCompact { get; set; }

    /// <summary>Identifies the <see cref="LastError"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderDropZone, DropZoneFileError> LastErrorProperty =
        AvaloniaProperty.RegisterDirect<AlderDropZone, DropZoneFileError>(nameof(LastError), owner => owner.LastError);

    /// <summary>The first error from the most recent selection or drop.</summary>
    public DropZoneFileError LastError { get => field; private set => SetAndRaise(LastErrorProperty, ref field, value); }

    /// <summary>The selected files and their current processing states. The application owns their storage handles.</summary>
    public ReadOnlyObservableCollection<AlderDropZoneFile> Files { get; }

    /// <summary>Receives <see cref="DropZoneFilesChangedEventArgs"/> after a selection or drop has been validated.</summary>
    [GeneratedStyledProperty]
    public partial ICommand? FilesChangedCommand { get; set; }

    /// <summary>Receives the <see cref="AlderDropZoneFile"/> removed from the list or replaced in single-file mode.</summary>
    [GeneratedStyledProperty]
    public partial ICommand? FileRemovedCommand { get; set; }

    /// <summary>Receives the failed <see cref="AlderDropZoneFile"/> when the user requests a retry.</summary>
    [GeneratedStyledProperty]
    public partial ICommand? RetryRequestedCommand { get; set; }

    /// <summary>Opens the system file picker.</summary>
    public ICommand BrowseCommand { get; }

    /// <summary>Removes the file record supplied as the command parameter.</summary>
    public ICommand RemoveFileCommand { get; }

    /// <summary>Requests a retry for the file record supplied as the command parameter.</summary>
    public ICommand RetryFileCommand { get; }

    /// <summary>Raised after a selection or drop has been validated.</summary>
    public event EventHandler<DropZoneFilesChangedEventArgs>? FilesChanged;

    /// <summary>Raised when a file is removed from the displayed collection.</summary>
    public event EventHandler<AlderDropZoneFile>? FileRemoved;

    /// <summary>Raised when the user requests another attempt to process a failed file.</summary>
    public event EventHandler<AlderDropZoneFile>? RetryRequested;

    private readonly ObservableCollection<AlderDropZoneFile> files = [];
    private readonly SemaphoreSlim validation = new(1);
    private Control? area;
    private bool picking;

    /// <summary>Initializes an empty drop zone.</summary>
    public AlderDropZone()
    {
        Files = new ReadOnlyObservableCollection<AlderDropZoneFile>(files);
        BrowseCommand = new FileAction(ignored => _ = BrowseAsync());
        RemoveFileCommand = new FileAction(file => { if (file is not null) RemoveFile(file); });
        RetryFileCommand = new FileAction(file =>
        {
            if (IsEffectivelyEnabled && file is { Status: DropZoneFileStatus.Failed } && files.Contains(file))
            {
                RetryRequested?.Invoke(this, file);
                ExecuteCommand(RetryRequestedCommand, file);
            }
        });
        files.CollectionChanged += (_, _) => PseudoClasses.Set(":has-files", files.Count > 0);
    }

    /// <summary>Opens the native picker and processes the chosen files through the drop zone's validation.</summary>
    public async Task BrowseAsync()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (picking || !IsEffectivelyEnabled) return;
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage)
        {
            ReportUnavailable();
            return;
        }

        picking = true;
        try
        {
            var selected = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Title,
                AllowMultiple = AllowMultiple,
                FileTypeFilter = FileTypeFilter
            });
            if (selected.Count > 0 && IsEffectivelyEnabled && this.IsAttachedToVisualTree())
                await AddFilesAsync(selected);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ReportUnavailable();
        }
        finally { picking = false; }
    }

    /// <summary>Adds storage items using the same limits and type filters as pointer-driven selection.</summary>
    public async Task AddFilesAsync(IEnumerable<IStorageItem> candidates, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        Dispatcher.UIThread.VerifyAccess();
        var incoming = candidates.ToArray();
        if (incoming.Length == 0) return;
        var added = new List<AlderDropZoneFile>();
        var rejected = new List<DropZoneRejectedFile>();
        var replaced = new List<AlderDropZoneFile>();
        await validation.WaitAsync(cancellationToken);
        try
        {
            bool multiple = AllowMultiple;
            int limit = MaxFiles > 0 ? MaxFiles : int.MaxValue;
            var maxSize = MaxFileSize;
            var patterns = FileTypeFilter?.SelectMany(filter => filter.Patterns ?? []).ToArray();
            foreach (var candidate in incoming)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var error = DropZoneFileError.None;
                if (candidate is not IStorageFile file || patterns is { Length: > 0 } &&
                    !patterns.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, candidate.Name, ignoreCase: true)))
                {
                    rejected.Add(new(candidate, DropZoneFileError.Type));
                    continue;
                }
                if (multiple ? files.Count + added.Count >= limit : added.Count > 0)
                {
                    rejected.Add(new(candidate, DropZoneFileError.Count));
                    continue;
                }

                ulong? size = null;
                try
                {
                    size = (await file.GetBasicPropertiesAsync()).Size;
                    if (maxSize is { } max)
                        error = size is null ? DropZoneFileError.Unavailable : size > max ? DropZoneFileError.Size : DropZoneFileError.None;
                }
                catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    if (maxSize is not null) error = DropZoneFileError.Unavailable;
                }
                if (error == DropZoneFileError.None) added.Add(new(file, size));
                else rejected.Add(new(candidate, error));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!multiple && added.Count > 0)
            {
                replaced.AddRange(files);
                files.Clear();
            }
            foreach (var file in added) files.Add(file);
            LastError = rejected.Count > 0 ? rejected[0].Error : DropZoneFileError.None;
        }
        finally { validation.Release(); }

        foreach (var file in replaced) NotifyFileRemoved(file);
        NotifyFilesChanged(new(added.ToArray(), rejected.ToArray()));
    }

    /// <summary>Removes a file record from the drop zone.</summary>
    public bool RemoveFile(AlderDropZoneFile file)
    {
        if (!IsEffectivelyEnabled || !files.Remove(file)) return false;
        NotifyFileRemoved(file);
        return true;
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (area is not null)
        {
            area.RemoveHandler(DragDrop.DragEnterEvent, OnDragOver);
            area.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
            area.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            area.RemoveHandler(DragDrop.DropEvent, OnDrop);
            area.RemoveHandler(PointerReleasedEvent, OnAreaRelease);
        }
        base.OnApplyTemplate(e);
        area = e.NameScope.Find<Control>("PART_DropArea");
        if (area is null) return;
        DragDrop.SetAllowDrop(area, true);
        area.AddHandler(DragDrop.DragEnterEvent, OnDragOver);
        area.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        area.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        area.AddHandler(DragDrop.DropEvent, OnDrop);
        area.AddHandler(PointerReleasedEvent, OnAreaRelease);
        PseudoClasses.Set(":drop-target", false);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
            PseudoClasses.Set(":drop-target", false);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        bool accepts = IsEffectivelyEnabled && e.DragEffects.HasFlag(DragDropEffects.Copy) &&
            e.DataTransfer.Formats.Contains(DataFormat.File);
        e.DragEffects = accepts ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        PseudoClasses.Set(":drop-target", accepts);
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => PseudoClasses.Set(":drop-target", false);

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        PseudoClasses.Set(":drop-target", false);
        if (!IsEffectivelyEnabled || e.DataTransfer.TryGetFiles() is not { Length: > 0 } items) return;
        e.DragEffects = e.DragEffects.HasFlag(DragDropEffects.Copy) ? DragDropEffects.Copy : DragDropEffects.None;
        if (e.DragEffects == DragDropEffects.None) return;
        e.Handled = true;
        await AddFilesAsync(items);
    }

    private async void OnAreaRelease(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left ||
            e.Source is Visual visual && (visual is Button || visual.FindAncestorOfType<Button>() is not null))
            return;
        await BrowseAsync();
    }

    private void ReportUnavailable()
    {
        LastError = DropZoneFileError.Unavailable;
        NotifyFilesChanged(new([], [new(null, LastError)]));
    }

    private void NotifyFilesChanged(DropZoneFilesChangedEventArgs args)
    {
        FilesChanged?.Invoke(this, args);
        ExecuteCommand(FilesChangedCommand, args);
    }

    private void NotifyFileRemoved(AlderDropZoneFile file)
    {
        FileRemoved?.Invoke(this, file);
        ExecuteCommand(FileRemovedCommand, file);
    }

    private static void ExecuteCommand(ICommand? command, object parameter)
    {
        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
    }

    private sealed class FileAction(Action<AlderDropZoneFile?> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter as AlderDropZoneFile);
    }
}
