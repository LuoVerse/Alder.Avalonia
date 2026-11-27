using System.Text.Json;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Localization;
using Alder.Avalonia.Gallery.Views;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class WorkflowEditorViewModel : GalleryViewModel
{
    private static readonly FilePickerFileType WorkflowJson = new("JSON")
    {
        Patterns = ["*.json"], MimeTypes = ["application/json"], AppleUniformTypeIdentifiers = ["public.json"]
    };

    private GalleryMessage? status;

    public WorkflowEditorViewModel()
    {
        SaveCommand = ReactiveCommand.Create<AlderWorkflowDefinition>(Save);
        ImportCommand = ReactiveCommand.CreateFromTask<AlderWorkflowEditor>(ImportAsync);
        ExportCommand = ReactiveCommand.CreateFromTask<AlderWorkflowEditor>(ExportAsync);
        TrialCommand = ReactiveCommand.CreateFromTask<AlderWorkflowEditor>(TrialAsync);
        ResetCommand = ReactiveCommand.Create(() =>
        {
            Workflow = WorkflowExampleData.Create();
            SetStatus(null);
        });
    }

    public AlderWorkflowDefinition? Workflow { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = WorkflowExampleData.Create();
    public IReadOnlyList<WorkflowAssigneeOption> Users => WorkflowExampleData.Users;
    public IReadOnlyList<WorkflowAssigneeOption> Roles => WorkflowExampleData.Roles;
    public IReadOnlyList<WorkflowConditionOption> Conditions => WorkflowExampleData.Conditions;
    public string Status => Display(status);
    public AlderWorkflowDefinition? SavedWorkflow { get; private set; }
    public ICommand SaveCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand TrialCommand { get; }
    public ICommand ResetCommand { get; }

    private void Save(AlderWorkflowDefinition definition)
    {
        SavedWorkflow = definition;
        SetStatus(new("WorkflowEditorSaved", definition.Name));
    }

    private async Task ImportAsync(AlderWorkflowEditor editor)
    {
        if (TopLevel.GetTopLevel(editor)?.StorageProvider is not { CanOpen: true } storage)
        {
            SetStatus("WorkflowEditorStorageUnavailable");
            return;
        }
        try
        {
            var files = await storage.OpenFilePickerAsync(new() { Title = Text("WorkflowEditorImport"), FileTypeFilter = [WorkflowJson] });
            using var file = files.FirstOrDefault();
            if (file is null) return;
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            Workflow = AlderWorkflowDefinition.FromJson(await reader.ReadToEndAsync());
            SetStatus(new("WorkflowEditorImported", file.Name));
        }
        catch (Exception error) when (error is JsonException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            SetStatus(new("WorkflowEditorFileError", error.Message));
        }
    }

    private async Task ExportAsync(AlderWorkflowEditor editor)
    {
        if (TopLevel.GetTopLevel(editor)?.StorageProvider is not { CanSave: true } storage)
        {
            SetStatus("WorkflowEditorStorageUnavailable");
            return;
        }
        try
        {
            string json = editor.GetWorkflow().ToJson();
            using var file = await storage.SaveFilePickerAsync(new()
            {
                Title = Text("WorkflowEditorExport"), SuggestedFileName = "workflow.json", DefaultExtension = "json",
                FileTypeChoices = [WorkflowJson], ShowOverwritePrompt = true
            });
            if (file is null) return;
            await using (var stream = await file.OpenWriteAsync())
            {
                if (stream.CanSeek) stream.SetLength(0);
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(json);
            }
            SetStatus(new("WorkflowEditorExported", file.Name));
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            SetStatus(new("WorkflowEditorFileError", error.Message));
        }
    }

    private async Task TrialAsync(AlderWorkflowEditor editor)
    {
        var errors = editor.ValidateWorkflow();
        Control content = errors.Count == 0
            ? new WorkflowTrialView(editor.GetWorkflow(), Conditions)
            : new AlderTextBlock { Text = string.Join(Environment.NewLine, errors), TextWrapping = TextWrapping.Wrap };
        await Dialog.ShowModalAsync(content, new DialogOptions { Title = Text("WorkflowEditorTrial"), MaxWidth = 520 }, owner: editor);
    }

    private void SetStatus(GalleryMessage? message)
    {
        status = message;
        this.RaisePropertyChanged(nameof(Status));
    }
}
