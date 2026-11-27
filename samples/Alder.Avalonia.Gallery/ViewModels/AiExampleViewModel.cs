using System.Collections.ObjectModel;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Alder.NativeMarkdown.UI;
using Alder.NativeMarkdown.UI.Transcript;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

/// <summary>Local interactive data for one AI example card. No network service is required.</summary>
public sealed partial class AiExampleViewModel : GalleryViewModel
{
    private CancellationTokenSource? response;
    private string? feedbackKey;
    private readonly HashSet<ChatAttachmentFile> pickedAttachments = [];
    private ChatAttachmentFile? attachment;
    private int attachmentGeneration;
    private static TopLevel? Owner => App.GalleryTopLevel;

    public string? Draft { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public bool VoicePreviewActive { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public bool Running { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
    public bool Submitting { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
    public string? FileName => attachment?.Name;
    public IImage? AttachmentPreview => attachment?.Preview;
    public string? Feedback => feedbackKey is null ? null : Text(feedbackKey);
    public object? Model { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public object? Effort { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = "High";
    private static readonly Dictionary<string, string> MentionNames = new()
    {
        ["@sarahjones"] = "Sarah Jones", ["@alexmiller"] = "Alex Miller", ["@davidkim"] = "David Kim",
        ["@ellagreen"] = "Ella Green", ["@miahernandez"] = "Mia Hernandez"
    };
    public string[] Tokens { get; } = [.. MentionNames.Keys, "/summarize", "/review"];
    public static IValueConverter TokenName { get; } = new FuncValueConverter<string, string>(value =>
        value is not null ? MentionNames.GetValueOrDefault(value, value) : string.Empty);
    public static IValueConverter TokenInitial { get; } = new FuncValueConverter<string, string>(value =>
        value is not null && MentionNames.TryGetValue(value, out var name) ? name[..1] : "/");
    public static IValueConverter HasPromptDraft { get; } = new FuncValueConverter<string, bool>(value => !string.IsNullOrWhiteSpace(value));
    private AiModelChoice[] standardModels => field ??=
    [
        new("Anthropic") { Text = "Claude Fable 5.2", Badge = Text("AiNewModel"), ShortText = "Fable 5.2", Description = Text("AiModelInfo0"), Intelligence = "5/5", Speed = "2/5", ContextWindow = "1,000,000" },
        new("Anthropic") { Text = "Claude Opus 5.5", ShortText = "Opus 5.5", Description = Text("AiModelInfo1"), Intelligence = "5/5", Speed = "3/5", ContextWindow = "1,000,000" },
        new("Anthropic") { Text = "Claude Sonnet 5.5", ShortText = "Sonnet 5.5", Description = Text("AiModelInfo2"), Intelligence = "4/5", Speed = "4/5", ContextWindow = "1,000,000" },
        new("Anthropic") { Text = "Claude Haiku 5.5", ShortText = "Haiku 5.5", Description = Text("AiModelInfo3"), Intelligence = "3/5", Speed = "5/5", ContextWindow = "500,000" },
        new("OpenAI") { Text = "GPT-6.1 Sol", ShortText = "GPT-6.1 Sol", Description = Text("AiModelInfo4"), Intelligence = "4/5", Speed = "3/5", ContextWindow = "1,050,000" },
    ];
    public AiModelChoice[] Models => Scenario switch
    {
        AiGalleryScenario.Models => extendedModels,
        AiGalleryScenario.Review => reviewModels,
        _ => standardModels
    };
    private static readonly string[] StandardModelPrices = ["$12 / $60", "$6 / $30", "$2 / $10", "$0.50 / $2.50", "$3 / $18"];
    private AiModelChoice[] extendedModels => field ??=
    [
        ..standardModels.Select((model, index) => new AiModelChoice
        {
            Text = model.Text, ShortText = model.ShortText, Group = model.Group, Image = model.Image, Badge = model.Badge,
            Description = model.Description, Intelligence = model.Intelligence, Speed = model.Speed,
            ContextWindow = model.ContextWindow, Price = StandardModelPrices[index]
        }),
        new("Google") { Text = "Gemini 4 Argon", Price = "$4 / $20" },
        new("Google") { Text = "Gemini 4 Flash", Price = "$0.30 / $1.20" },
        new("xAI") { Text = "Grok 4.7", Price = "$3 / $15" },
        new("Moonshot AI") { Text = "Kimi K3.1 Pro", Price = "$1 / $4" }
    ];
    private AiModelChoice[] reviewModels => field ??=
    [
        new("Cursor", grouped: false) { Text = "Composer 2.5 Fast" },
        new("Anthropic", grouped: false) { Text = "Opus 4.8 1M High" },
        new("OpenAI", grouped: false) { Text = "GPT-5.5 1M High" },
        new("Anthropic", grouped: false) { Text = "Sonnet 4.6 1M Medium" },
        new("OpenAI", grouped: false) { Text = "Codex 5.3 High Fast" },
        new("Anthropic", grouped: false) { Text = "Opus 4.6 1M High" },
        new("Google", grouped: false) { Text = "Gemini 3.1 Pro" }
    ];
    public string[] Efforts { get; } = ["Low", "Medium", "High", "Max", "Ultra"];
    public ObservableCollection<TranscriptEntry> Messages => field ??= CreateMessages();
    public ObservableCollection<AiQueuedPrompt> Queue { get; } = [];
    public string ExampleCode => """
        string Greet(string name)
        {
            return $"Hello, {name}!";
        }

        Console.WriteLine(Greet("Avalonia"));
        """;
    public string WeatherJson => "{\n  \"summary\": \"18°C, partly cloudy\"\n}";
    public string MessageMarkdown => Text("AiMessageMdIntro") + "\n\n```csharp\npublic enum ChatStatus { Ready, Streaming }\n```\n\n" + Text("AiMessageMdEnd");
    public string ExampleMarkdown => "# " + Text("AiMdTitle") + "\n\n" + Text("AiMdBody") + "\n\n## " + Text("AiFeatures") +
        "\n- " + Text("AiMdList") + "\n- " + Text("AiInlineFeature") +
        "\n- " + Text("AiFenceFeature") + "\n\n```axaml\n<alder:AlderChatMessage Avatar=\"AI\">\n  <alder:AlderMarkdown Markdown=\"{CompiledBinding Response}\" />\n</alder:AlderChatMessage>\n```\n\n> " + Text("AiSdkEnding");
    private string Bullets(string key) => string.Join("\n", Text(key).Split(';', '；').Select(item => "- " + item));
    private string StreamingMarkdown => Scenario == AiGalleryScenario.RepairMarkdown
        ? "# " + Text("AiRepairDemo") + "\n\n" + Text("AiRepairSentence") + "\n\n" + Bullets("AiRepairItems") +
            "\n\n```csharp\nvar stream = markdown.BeginStream();\nstream.Push(responseChunk);\nstream.Complete();\n```"
        : "# " + Text("AiStreamDemo") + "\n\n" + Text("AiStreamSentence") + "\n\n" + string.Join("\n", Text("AiStreamItems").Split(';', '；').Select((item, index) => $"{index + 1}. {item}")) +
            "\n\n```csharp\nConsole.WriteLine(\"Streaming Markdown in Avalonia\");\n```";
    public string[] Threads => [Text("AiLaunch"), Text("AiCustomer"), Text("AiEditing")];

    public Alder.NativeMarkdown.UI.MarkdownStream? PreviewStream { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
    public AiDecision Decision
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsApprovalPending));
        }
    }
    public bool IsApprovalPending => Decision == AiDecision.Pending;
    public ICommand ApproveTool => field ??= ReactiveCommand.Create(() => { Decision = AiDecision.Approved; });
    public ICommand RejectTool => field ??= ReactiveCommand.Create(() => { Decision = AiDecision.Rejected; });
    public KbdKey ApprovalModifier => OperatingSystem.IsMacOS() ? KbdKey.Command : KbdKey.Ctrl;
    private int question;
    private readonly Dictionary<int, (string? Choice, string? Other)> approvalAnswers = [];
    private readonly HashSet<int> skippedQuestions = [];
    private bool FlightWizard => Scenario == AiGalleryScenario.Flight;
    public int StepCount => FlightWizard ? 2 : 3;
    private string QuestionAt(int index) => Text(FlightWizard ? (index == 0 ? "AiFlightChoice" : "AiExtras") :
        index switch { 0 => "AiQuestion", 1 => "AiSignPages", _ => "AiSessions" });
    public string Question => QuestionAt(question);
    public string[] QuestionSteps => Enumerable.Range(0, StepCount).Select(QuestionAt).ToArray();
    public int ActiveQuestion
    {
        get => question;
        set
        {
            if (value < 0 || value >= StepCount || value == question || Decision != AiDecision.Pending) return;
            RememberAnswer();
            question = value;
            RefreshQuestion();
        }
    }
    public string QuestionNumber => $"{question + 1}/{StepCount}";
    public string NextLabel => Text(question + 1 == StepCount ? "AiSubmit" : "AiContinue");
    public bool LastQuestion => question + 1 == StepCount;
    public bool Dismissed { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
    public ICommand Dismiss { get; }
    public ICommand Restart { get; }
    public bool SourcesExpanded { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }

    private IImage AvaloniaLogo => field ??= LoadCitationLogo("avalonia");
    private IImage DotNetLogo => field ??= LoadCitationLogo("dotnet");
    private IImage NuGetLogo => field ??= LoadCitationLogo("nuget");

    public AiCitation[] Citations => field ??=
    [
        Citation("Avalonia", "https://avaloniaui.net/", AvaloniaLogo),
        Citation(".NET", "https://dotnet.microsoft.com/en-us/", DotNetLogo),
        Citation("NuGet", "https://www.nuget.org/packages/Avalonia", NuGetLogo),
        Citation("Avalonia XAML", "https://docs.avaloniaui.net/docs/xaml", AvaloniaLogo),
        Citation("Avalonia Styles", "https://docs.avaloniaui.net/docs/styling/styles", AvaloniaLogo)
    ];

    public AiCitation AvaloniaDocsCitation => field ??= Citation("Avalonia Docs", "https://docs.avaloniaui.net/", AvaloniaLogo);
    public AiCitation AvaloniaReleaseCitation => field ??= Citation("Avalonia 12 release notes", "https://github.com/AvaloniaUI/Avalonia/releases/tag/12.0.0", AvaloniaLogo);
    public AiCitation AvaloniaUpgradeCitation => field ??= Citation("Avalonia 12 upgrade guide", "https://docs.avaloniaui.net/docs/avalonia12-breaking-changes", AvaloniaLogo);

    private AiCitation Citation(string title, string url, IImage logo) => new(title, new Uri(url), logo, OpenSource);

    private static Bitmap LoadCitationLogo(string name)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://Alder.Avalonia.Gallery/Assets/Card/ai-{name}.png"));
        return new Bitmap(stream);
    }

    public int CurrentStep => question + 1;
    public string QuestionProgressLabel => Format("AiQuestionProgress", CurrentStep, StepCount);
    public string? SkippedLabel
    {
        get
        {
            if (!LastQuestion) return null;
            int count = skippedQuestions.Count - (skippedQuestions.Contains(question) ? 1 : 0);
            return count > 0 ? Format("AiSkippedCount", count) : null;
        }
    }
    public bool HasAnswer => SelectedChoice is not null || !string.IsNullOrWhiteSpace(OtherAnswer);

    public string? OtherAnswer
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            if (!string.IsNullOrWhiteSpace(value)) SelectedChoice = null;
            this.RaisePropertyChanged(nameof(HasAnswer));
        }
    }

    public string? SelectedChoice
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            if (value is not null) OtherAnswer = null;
            this.RaisePropertyChanged(nameof(HasAnswer));
        }
    }
    public AiApprovalChoice[] Options => (FlightWizard, question) switch
    {
        (true, 0) => [Choice("AiFlightEarly","AiEarlyHint"), Choice("AiFlightStop","AiStopHint"), Choice("AiFlightNight","AiNightHint")],
        (true, _) => [Choice("AiBag","AiBagHint"), Choice("AiSeat","AiSeatHint"), Choice("AiPickup","AiPickupHint")],
        (false, 0) => [Choice("AiPasskeys", "AiPasskeysHint"), Choice("AiMagic", "AiMagicHint"), Choice("AiGoogle", "AiGoogleHint")],
        (false, 1) => [Choice("AiDashboard","AiDashboardHint"), Choice("AiSettings","AiSettingsHint"), Choice("AiAdmin","AiAdminHint")],
        _ => [Choice("AiDatabase","AiDatabaseHint"), Choice("AiCookie","AiCookieHint"), Choice("AiRedis","AiRedisHint")]
    };
    public AiGalleryScenario Scenario
    {
        get => field;
        set
        {
            field = value;
            if (value == AiGalleryScenario.Mentions) Draft = Text("AiMentionDraft");
            if (value == AiGalleryScenario.Queue && Queue.Count == 0) SeedQueue();
            if (value == AiGalleryScenario.Effort) { Model = Models[2]; Effort = "Max"; }
            if (value == AiGalleryScenario.ModelUsage) Effort = "Ultra";
            if (value == AiGalleryScenario.Models) Effort = "Medium";
            if (value is AiGalleryScenario.Review or AiGalleryScenario.Models) Model = Models[value == AiGalleryScenario.Review ? 0 : 1];
        }
    }
    public AiRunStatus TraceStatus { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = AiRunStatus.Running;
    public string TraceText { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = "";
    public bool TraceExpanded { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public string? TraceDuration { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
    public string? TraceAnswer { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
    public bool IsTraceActive { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
    public bool Finished { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
    private CancellationTokenSource? preview;
    public void CancelPreview() => preview?.Cancel();

    internal async Task PlayPreviewAsync(bool reducedMotion)
    {
        CancelPreview();
        using var job = new CancellationTokenSource();
        preview = job;
        var token = job.Token;
        try
        {
            Finished = false;
            switch (Scenario)
            {
                case AiGalleryScenario.Markdown:
                case AiGalleryScenario.RepairMarkdown:
                    bool animateTail = Scenario == AiGalleryScenario.RepairMarkdown && !reducedMotion;
                    var stream = AlderMarkdown.ChatServices.CreateStream(smooth: false, fadeDuration: animateTail ? TimeSpan.FromMilliseconds(180) : TimeSpan.Zero);
                    stream.ShowCaret = animateTail;
                    PreviewStream = stream;
                    try
                    {
                        string text = StreamingMarkdown;
                        if (reducedMotion)
                            stream.Push(text);
                        else
                        {
                            for (int i = 0; i < text.Length;)
                            {
                                int length = Math.Min(8, text.Length - i);
                                if (i + length < text.Length && char.IsHighSurrogate(text[i + length - 1])) length++;
                                stream.Push(text.Substring(i, length));
                                i += length;
                                await Task.Delay(120, token);
                            }
                        }
                    }
                    finally { stream.Complete(); }
                    break;
                case AiGalleryScenario.Chain:
                    await PlayChainAsync(reducedMotion, token);
                    break;
                case AiGalleryScenario.Reasoning:
                case AiGalleryScenario.ReasoningSdk:
                    await PlayReasoningAsync(job, Scenario == AiGalleryScenario.ReasoningSdk);
                    break;
            }
            Finished = true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally { if (ReferenceEquals(preview, job)) preview = null; }
    }

    private async Task PlayReasoningAsync(CancellationTokenSource job, bool withAnswer)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        TraceText = "";
        TraceAnswer = null;
        TraceDuration = null;
        TraceExpanded = false;
        TraceStatus = AiRunStatus.Running;
        IsTraceActive = true;
        try
        {
            await Task.Delay(withAnswer ? 1900 : 1500, job.Token);
            await StreamReasoningSample(Text(withAnswer ? "AiReasoningSdkTrace" : "AiTraceSummary"), 18, 85,
                text => TraceText = text, job.Token);
            TraceDuration = string.Format(Text("AiReasoningSeconds"), withAnswer ? 4 : 3);
            TraceStatus = AiRunStatus.Completed;
            if (withAnswer)
                await StreamReasoningSample(Text("AiAvalonia12Answer"), 13, 68, text => TraceAnswer = text, job.Token);
        }
        catch (OperationCanceledException) when (job.IsCancellationRequested && ReferenceEquals(preview, job))
        {
            if (TraceStatus == AiRunStatus.Running)
            {
                TraceDuration = string.Format(Text("AiReasoningSeconds"), Math.Max(1, (int)Math.Round(started.Elapsed.TotalSeconds)));
                TraceStatus = AiRunStatus.Stopped;
            }
            throw;
        }
        finally
        {
            if (ReferenceEquals(preview, job)) IsTraceActive = false;
        }
    }

    private static async Task StreamReasoningSample(string source, int steps, int interval, Action<string> publish, CancellationToken token)
    {
        for (int i = 1; i <= steps; i++)
        {
            await Task.Delay(interval, token);
            int end = (source.Length * i + steps - 1) / steps;
            if (end < source.Length && char.IsHighSurrogate(source[end - 1])) end++;
            publish(source[..end]);
        }
    }

    private AiApprovalChoice Choice(string title, string description) => new(Text(title), Text(description));

    public ICommand Next { get; }
    public ICommand SkipQuestion { get; }
    public ICommand Replay { get; }
    public ICommand Send { get; }
    public ICommand Stop { get; }
    public ICommand Suggest { get; }
    public ICommand Like { get; }
    public ICommand Dislike { get; }
    public ICommand Retry { get; }
    public ICommand OpenSource { get; }
    public ICommand Act { get; }
    public ICommand Copy { get; }
    public ICommand Attach { get; }
    public ICommand Remove { get; }
    public ICommand ResetQueue { get; }
    public ICommand Enqueue { get; }

    public AiExampleViewModel()
    {
        Queue.CollectionChanged += (_, _) => ReleaseUnusedAttachments();
        Model = Models[1];
        Dismiss = ReactiveCommand.Create(() => Dismissed = true);
        Restart = ReactiveCommand.Create(() =>
        {
            approvalAnswers.Clear();
            skippedQuestions.Clear();
            question = 0;
            Dismissed = false;
            Decision = AiDecision.Pending;
            RefreshQuestion();
        });
        Next = ReactiveCommand.Create(() => AdvanceQuestion(skip: false));
        SkipQuestion = ReactiveCommand.Create(() => AdvanceQuestion(skip: true));
        Replay = ReactiveCommand.CreateFromTask(() => PlayPreviewAsync(Owner is { } window && AlderMotion.GetReduced(window)));
        Send = ReactiveCommand.CreateFromTask<string?>(SendAsync);
        Stop = ReactiveCommand.Create(() => { response?.Cancel(); CancelPreview(); });
        Suggest = ReactiveCommand.Create<string>(value => Draft = value);
        Like = ReactiveCommand.Create<string?>(_ => Notify("AiLiked"));
        Dislike = ReactiveCommand.Create<string?>(_ => Notify("AiDisliked"));
        Retry = ReactiveCommand.CreateFromTask<string?>(async _ => { await SendAsync(Text("AiChatQuestion")); Notify("AiRegenerated"); });
        OpenSource = ReactiveCommand.CreateFromTask<string>(async source =>
        {
            if (Owner is { } owner && Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                await owner.Launcher.LaunchUriAsync(uri);
        });
        Act = ReactiveCommand.Create<string?>(key => Notify(key ?? "AiCompleted"));
        Copy = ReactiveCommand.CreateFromTask<string?>(async value =>
        {
            if (Owner?.Clipboard is { } clipboard) await clipboard.SetTextAsync(value ?? Text("AiAssistantReply"));
            Notify("AiCopied");
        });
        Attach = ReactiveCommand.CreateFromTask(PickAttachmentAsync);
        Remove = ReactiveCommand.Create(() => { attachmentGeneration++; SetAttachment(null); });
        ResetQueue = ReactiveCommand.Create(() =>
        {
            attachmentGeneration++;
            SetAttachment(null);
            SeedQueue();
            Draft = "";
        });
        Enqueue = ReactiveCommand.Create<string?>(value =>
        {
            if (string.IsNullOrWhiteSpace(value) && attachment is null) return;
            Queue.Add(new AiQueuedPrompt(value ?? "", attachment));
            SetAttachment(null);
            Draft = "";
        });
    }

    private void RememberAnswer()
    {
        if (!HasAnswer)
        {
            approvalAnswers.Remove(question);
            return;
        }
        skippedQuestions.Remove(question);
        approvalAnswers[question] = (SelectedChoice, OtherAnswer);
    }

    private void AdvanceQuestion(bool skip)
    {
        if (skip)
        {
            approvalAnswers.Remove(question);
            skippedQuestions.Add(question);
        }
        else RememberAnswer();
        if (LastQuestion) return;
        question++;
        Decision = AiDecision.Pending;
        RefreshQuestion();
    }

    private void RefreshQuestion()
    {
        this.RaisePropertyChanged(nameof(Options));
        SelectedChoice = null;
        OtherAnswer = null;
        if (approvalAnswers.TryGetValue(question, out var answer))
        {
            SelectedChoice = answer.Choice;
            OtherAnswer = answer.Other;
        }
        this.RaisePropertyChanged(nameof(NextLabel));
        this.RaisePropertyChanged(nameof(Question));
        this.RaisePropertyChanged(nameof(QuestionNumber));
        this.RaisePropertyChanged(nameof(CurrentStep));
        this.RaisePropertyChanged(nameof(QuestionProgressLabel));
        this.RaisePropertyChanged(nameof(LastQuestion));
        this.RaisePropertyChanged(nameof(ActiveQuestion));
        this.RaisePropertyChanged(nameof(SkippedLabel));
    }

    private async Task PickAttachmentAsync()
    {
        if (Owner?.StorageProvider is not { CanOpen: true } storage) return;
        int generation = attachmentGeneration;
        IReadOnlyList<IStorageFile> files;
        try
        {
            files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Text("AiChooseAttachments"),
                AllowMultiple = false
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return;
        }

        try
        {
            if (generation != attachmentGeneration || files.Count == 0) return;
            var picked = await ChatAttachmentFile.ReadAsync(files[0]);
            if (generation == attachmentGeneration)
            {
                pickedAttachments.Add(picked);
                SetAttachment(picked);
            }
            else
                picked.Preview?.Dispose();
        }
        finally
        {
            foreach (var file in files) file.Dispose();
        }
    }

    private void SetAttachment(ChatAttachmentFile? value)
    {
        if (ReferenceEquals(attachment, value)) return;
        attachment = value;
        this.RaisePropertyChanged(nameof(AttachmentPreview));
        this.RaisePropertyChanged(nameof(FileName));
        ReleaseUnusedAttachments();
    }

    private void ReleaseUnusedAttachments()
    {
        // A queued prompt and its duplicates can reference the same bitmap as the draft.
        pickedAttachments.RemoveWhere(file =>
        {
            if (ReferenceEquals(attachment, file) || Queue.Any(item => ReferenceEquals(item.Attachment, file)))
                return false;
            file.Preview?.Dispose();
            return true;
        });
    }

    private void SeedQueue()
    {
        Queue.Clear();
        foreach (var key in new[] { "AiQueueOne", "AiQueueTwo", "AiQueueThree" })
            Queue.Add(new AiQueuedPrompt(Text(key)));
        Queue.Add(new AiQueuedPrompt("", new ChatAttachmentFile("mockup.png", null, null)));
    }

    internal void EditQueued(AiQueuedPrompt item)
    {
        if (!Queue.Contains(item)) return;
        attachmentGeneration++;
        SetAttachment(item.Attachment);
        Queue.Remove(item);
        Draft = item.Text;
    }

    private ContentEntry Assistant(string text, bool actions = false)
    {
        var markdown = ConversationMarkdown(text);
        return new ContentEntry(AssistantMessage(markdown, markdown, text, actions));
    }

    private static AlderMarkdown ConversationMarkdown(string? text = null)
    {
        var markdown = new AlderMarkdown { Markdown = text };
        markdown.Resources[MarkdownResourceKeys.BlockSpacing] = 4d;
        return markdown;
    }

    private AlderChatMessage AssistantMessage(Control content, AlderMarkdown markdown, string text, bool actions)
    {
        var message = new AlderChatMessage { Avatar = "AI", Content = content };
        if (actions)
            message.Actions = new AlderChatMessageActions
            {
                CopyCommand = Copy,
                CommandParameter = text,
                RetryCommand = ReactiveCommand.CreateFromTask<string?>(_ => StreamReply(markdown, message, text),
                    this.WhenAnyValue(model => model.Running, running => !running))
            };
        return message;
    }

    private ContentEntry User(string text) => new(new AlderChatMessage { IsUser = true, Content = text });
    private AlderChainOfThoughtStep CompletedStep(string title, string detail) => new()
    {
        Header = Text(title), Description = Text(detail), Status = AiRunStatus.Completed
    };

    private ObservableCollection<TranscriptEntry> CreateMessages()
    {
        if (Scenario != AiGalleryScenario.FullChat)
            return [User(Text("AiChatQuestion")), Assistant(Text("AiChatAnswer")), User(Text("AiChatFollowup")),
                Assistant(Text("AiChatFollowupAnswer")), User(Text("AiConsuming")), Assistant(Text("AiRichReply"))];

        var outline = Text("AiSettingsOutline") + "\n\n" + Bullets("AiSettingsList");
        var markdown = ConversationMarkdown(outline);
        var reply = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new AlderChainOfThought
                {
                    Header = Text("AiConversationThought"), Status = AiRunStatus.Completed, IsExpanded = true,
                    Content = new AlderChainOfThoughtSteps { Children = { CompletedStep("AiReadReq", "AiMapReq"), CompletedStep("AiPlanResponsive", "AiResponsiveDetail") } }
                },
                markdown
            }
        };
        return [User(Text("AiFoundSettings")), new ContentEntry(AssistantMessage(reply, markdown, outline, actions: true)),
            User(Text("AiDrawerQ")), Assistant(Text("AiDrawerA") + " " + Text("AiDrawerLast"), actions: true)];
    }

    private async Task SendAsync(string? prompt)
    {
        if (Running || Submitting || string.IsNullOrWhiteSpace(prompt)) return;
        Draft = "";
        if (Scenario == AiGalleryScenario.PromptRun)
        {
            using var run = new CancellationTokenSource();
            response = run;
            Submitting = true;
            try
            {
                await Task.Delay(450, run.Token);
                Running = true;
                Submitting = false;
                await Task.Delay(2600, run.Token);
            }
            catch (OperationCanceledException) when (run.IsCancellationRequested) { }
            finally
            {
                Running = false;
                Submitting = false;
                response = null;
            }
            return;
        }
        Messages.Add(User(prompt));
        var reply = Text("AiChatAnswer");
        var markdown = ConversationMarkdown();
        var message = AssistantMessage(markdown, markdown, reply, actions: Scenario == AiGalleryScenario.FullChat);
        Messages.Add(new ContentEntry(message));
        await StreamReply(markdown, message, reply);
    }

    private async Task StreamReply(AlderMarkdown markdown, AlderChatMessage message, string reply)
    {
        if (Running) return;
        using var cancellation = new CancellationTokenSource();
        response = cancellation;
        Running = true;
        var actions = message.Actions as AlderChatMessageActions;
        if (actions is not null) actions.IsEnabled = false;
        var stream = markdown.BeginStream();
        int written = 0;
        try
        {
            while (written < reply.Length)
            {
                int count = Math.Min(5, reply.Length - written);
                if (written + count < reply.Length && char.IsHighSurrogate(reply[written + count - 1])) count++;
                stream.Push(reply.Substring(written, count));
                written += count;
                await Task.Delay(32, cancellation.Token);
            }
            Notify("AiCompleted");
        }
        catch (OperationCanceledException) { Notify("AiStopped"); }
        finally
        {
            stream.Complete();
            if (actions is not null)
            {
                actions.CommandParameter = reply[..written];
                actions.IsEnabled = true;
            }
            response = null;
            Running = false;
        }
    }

    private void Notify(string key)
    {
        feedbackKey = key;
        this.RaisePropertyChanged(nameof(Feedback));
    }
}

/// <summary>Localized title and supporting text for an approval wizard choice.</summary>
public sealed record AiApprovalChoice(string Title, string Description);

/// <summary>Selects the interaction demonstrated by an individual Gallery preview.</summary>
public enum AiGalleryScenario { Default, Flight, Markdown, RepairMarkdown, Chain, Reasoning, FullChat, Mentions, Queue, Effort, Review, Models, ModelUsage, PromptRun, ReasoningSdk }


/// <summary>A queued prompt has an identity even when two drafts contain the same text.</summary>
public sealed class AiQueuedPrompt(string text, ChatAttachmentFile? attachment = null)
{
    public string Text { get; } = text;
    public ChatAttachmentFile? Attachment { get; } = attachment;
    public bool HasText => !string.IsNullOrWhiteSpace(Text);
}


/// <summary>Provider and optional release badge shown in the model popup.</summary>
public sealed class AiModelChoice : AlderPickerOption
{
    public AiModelChoice(string? provider = null, bool grouped = true)
    {
        Group = grouped ? provider : null;
        var (key, color) = provider switch
        {
            "Anthropic" => ("AlderAiSpark", 0xFFC57756u),
            "OpenAI" => ("AlderIconGlobe", 0xFF71717Au),
            "Google" => ("AlderIconStarOutline", 0xFF5785CDu),
            "xAI" => ("AlderIconArrowUpRight", 0xFF81818Bu),
            "Moonshot AI" => ("AlderIconMoon", 0xFF8C76C3u),
            "Cursor" => ("AlderIconPencil", 0xFF81818Bu),
            _ => ((string?)null, 0u)
        };
        if (key is not null && global::Avalonia.Application.Current?.TryFindResource(key, out var resource) == true &&
            resource is Geometry geometry)
        {
            Image = new DrawingImage
            {
                Drawing = new GeometryDrawing { Geometry = geometry, Brush = new SolidColorBrush(Color.FromUInt32(color)) }
            };
        }
    }

    public string? Badge { get; init; }
    public string? Price { get; init; }
    public string? Intelligence { get; init; }
    public string? Speed { get; init; }
    public string? ContextWindow { get; init; }
}
