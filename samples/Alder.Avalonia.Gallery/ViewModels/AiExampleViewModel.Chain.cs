using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed partial class AiExampleViewModel
{
    public AiTracePhase ThinkingPhase => field ??= new(Text("AiThinking"));
    public AiTracePhase ExplorePhase => field ??= new(Text("AiExploreRunning"), Text("AiExplored"),
        new(FileTitle("AiReadingFile", "Views/MainWindow.axaml"), FileTitle("AiReadFile", "Views/MainWindow.axaml")),
        new(FileTitle("AiReadingFile", "Themes/AppStyles.axaml"), FileTitle("AiReadFile", "Themes/AppStyles.axaml")),
        new(FileTitle("AiReadingFile", "Themes/FormFields.axaml"), FileTitle("AiReadFile", "Themes/FormFields.axaml"), Text("AiInputExports")));
    public AiTracePhase EditPhase => field ??= new(Text("AiEditRunning"), Text("AiEditedFiles"),
        new(FileTitle("AiCreatingFile", "Views/LoginView.axaml"), FileTitle("AiCreatedFile", "Views/LoginView.axaml"), Text("AiLoginFields")),
        new(FileTitle("AiUpdatingFile", "Views/MainWindow.axaml"), FileTitle("AiUpdatedFile", "Views/MainWindow.axaml"), Text("AiLoginLink")));
    public AiTracePhase CheckPhase => field ??= new(Text("AiCheckRunning"), Text("AiChecked"),
        new(Text("AiTypecheckActive"), Text("AiTypecheck"), Text("AiTypecheckPassed")),
        new(Text("AiLintActive"), Text("AiLintDone"), Text("AiLintPassed")));

    private string FileTitle(string key, string path) => Format(key, path);

    private async Task PlayChainAsync(bool reducedMotion, CancellationToken token)
    {
        ThinkingPhase.Open();
        ExplorePhase.Reset();
        EditPhase.Reset();
        CheckPhase.Reset();
        TraceText = "";

        string summary = Text("AiLoginTrace");
        await Task.Delay(720, token);
        if (!reducedMotion)
        {
            for (int end = 5; end < summary.Length; end += 5)
            {
                TraceText = summary[..end];
                await Task.Delay(65, token);
            }
        }
        TraceText = summary;
        await Task.Delay(580, token);
        ThinkingPhase.Finish();

        foreach (var phase in new[] { ExplorePhase, EditPhase, CheckPhase })
        {
            phase.Open();
            foreach (var step in phase.Steps)
            {
                step.Status = AiRunStatus.Running;
                await Task.Delay(820, token);
                step.Status = AiRunStatus.Completed;
            }
            phase.Finish();
        }
    }
}

/// <summary>One collapsible phase in the simulated Gallery run.</summary>
public sealed class AiTracePhase : ReactiveObject
{
    private readonly string runningTitle;
    private readonly string? finishedTitle;

    public AiTracePhase(string runningTitle, string? finishedTitle = null, params AiTraceStep[] steps)
    {
        this.runningTitle = runningTitle;
        this.finishedTitle = finishedTitle;
        Steps = steps;
        Title = runningTitle;
    }

    public AiTraceStep[] Steps { get; }
    public AiRunStatus Status { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = AiRunStatus.Running;
    public bool Visible { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
    public bool Expanded { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public string Title { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }

    internal void Open()
    {
        Reset();
        Expanded = true;
        Visible = true;
    }

    internal void Reset()
    {
        Visible = false;
        Status = AiRunStatus.Running;
        Title = runningTitle;
        foreach (var step in Steps) step.Status = AiRunStatus.Pending;
    }

    internal void Finish()
    {
        Title = finishedTitle ?? runningTitle;
        Status = AiRunStatus.Completed;
    }
}

/// <summary>A simulated action that is revealed at the start of its run.</summary>
public sealed class AiTraceStep(string runningTitle, string finishedTitle, string? detail = null) : ReactiveObject
{
    public AiRunStatus Status
    {
        get => field;
        internal set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Title));
            this.RaisePropertyChanged(nameof(Description));
            this.RaisePropertyChanged(nameof(Visible));
        }
    }

    public bool Visible => Status != AiRunStatus.Pending;
    public string Title => Status == AiRunStatus.Completed ? finishedTitle : runningTitle;
    public string? Description => Status == AiRunStatus.Completed ? detail : null;
}
