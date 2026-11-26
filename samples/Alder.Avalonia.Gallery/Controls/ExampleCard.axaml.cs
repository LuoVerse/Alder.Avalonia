using Alder.Avalonia.Controls;
using Alder.NativeMarkdown.UI.Extensions;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace Alder.Avalonia.Gallery.Controls;

public partial class ExampleCard : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ExampleCard, string>(nameof(Title), "Example");
    public static readonly StyledProperty<object?> PreviewProperty =
        AvaloniaProperty.Register<ExampleCard, object?>(nameof(Preview));
    public static readonly StyledProperty<object?> FeedbackProperty =
        AvaloniaProperty.Register<ExampleCard, object?>(nameof(Feedback));
    public static readonly DirectProperty<ExampleCard, bool> HasFeedbackProperty =
        AvaloniaProperty.RegisterDirect<ExampleCard, bool>(nameof(HasFeedback), card => card.HasFeedback);
    public static readonly StyledProperty<string> AxamlSourceProperty =
        AvaloniaProperty.Register<ExampleCard, string>(nameof(AxamlSource), "");
    public static readonly StyledProperty<string> CSharpSourceProperty =
        AvaloniaProperty.Register<ExampleCard, string>(nameof(CSharpSource), "");

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public object? Preview { get => GetValue(PreviewProperty); set => SetValue(PreviewProperty, value); }
    public object? Feedback { get => GetValue(FeedbackProperty); set => SetValue(FeedbackProperty, value); }
    public bool HasFeedback => Feedback is not null;
    public string AxamlSource { get => GetValue(AxamlSourceProperty); set => SetValue(AxamlSourceProperty, value); }
    public string CSharpSource { get => GetValue(CSharpSourceProperty); set => SetValue(CSharpSourceProperty, value); }

    private static readonly MarkdownServices SourceServices = new()
    {
        Highlighter = AlderMarkdown.ChatServices.Highlighter,
        CodeBlocks = AlderMarkdown.ChatServices.CodeBlocks with { ShowHeader = false, ShowLineNumbers = true }
    };

    private bool ready;
    private int sourceRevision;
    private Action? prepareSources;

    public ExampleCard()
    {
        InitializeComponent();
        MarkdownServices.SetServices(SourceCode, SourceServices);
        ready = true;
        SizeChanged += (_, e) =>
        {
            if (Classes.Contains("chat-message-preview"))
                Classes.Set("compact-preview", e.NewSize.Width < 640);
            bool narrow = e.NewSize.Width < 410;
            Grid.SetRow(SegmentBar, narrow ? 1 : 0);
            Grid.SetColumn(SegmentBar, narrow ? 0 : 1);
            Grid.SetColumnSpan(SegmentBar, narrow ? 2 : 1);
            SegmentBar.HorizontalAlignment = narrow ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            SegmentBar.Margin = narrow ? new Thickness(0, 9, 0, 0) : default;
        };
    }

    internal void SetSources(string axaml, string viewModel, string codeBehind, string controlUsage)
    {
        AxamlSource = axaml;
        CSharpSource = controlUsage;
        CodeKinds.Items.Clear();
        AddCodeKind("ViewModelSource", viewModel);
        AddCodeKind("CodeBehindSource", codeBehind);
        AddCodeKind("ControlUsageSource", controlUsage);
        CodeKinds.SelectedIndex = 0;
    }

    internal void PrepareSourcesOnDemand(Action prepare) => prepareSources = prepare;

    internal void ShowAxaml() => Modes.SelectedIndex = 1;

    internal void SetSourceFiles((string Name, string Text)[] axaml, (string Name, string Text)[] csharp)
    {
        AxamlSource = axaml[0].Text;
        CSharpSource = csharp[0].Text;
        SetFiles(AxamlKinds, axaml);
        SetFiles(CodeKinds, csharp);
    }

    private static void SetFiles(ListBox tabs, (string Name, string Text)[] files)
    {
        tabs.Items.Clear();
        foreach (var (name, text) in files)
            tabs.Items.Add(new AlderListBoxItem { Content = name, Tag = text });
        tabs.SelectedIndex = 0;
    }

    private void AddCodeKind(string resource, string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return;
        var item = new AlderListBoxItem { Tag = source };
        item[!ContentControl.ContentProperty] = new DynamicResourceExtension("GalleryText." + resource);
        CodeKinds.Items.Add(item);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FeedbackProperty)
            RaisePropertyChanged(HasFeedbackProperty, change.GetOldValue<object?>() is not null, HasFeedback);
        if (ready && (change.Property == AxamlSourceProperty || change.Property == CSharpSourceProperty))
            Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Refresh();
    }

    private void ModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!ready) return;
        var owner = Parent is GalleryExampleSection section ? section.Parent : Parent;
        if (e.AddedItems.Count > 0 && e.RemovedItems.Count > 0 && owner is GalleryExampleList list)
            list.PreserveExamplePosition(this);
        Refresh();
    }

    private async void CopyCode(object? sender, RoutedEventArgs e)
    {
        var code = SourceCode.Code;
        if (Modes.SelectedIndex <= 0 || string.IsNullOrEmpty(code)) return;
        int revision = sourceRevision;
        bool copied = false;
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(code);
                copied = true;
            }
        }
        catch (Exception error) when (error is InvalidOperationException or ExternalException)
        {
            copied = false;
        }
        if (sourceRevision == revision && CodePanel.IsVisible)
            SetCopyFeedback(copied);
    }

    private void SetCopyFeedback(bool? copied = null)
    {
        CopyCodeButton.Classes.Set("copied", copied == true);
        CopyCodeButton.Classes.Set("copyFailed", copied == false);
    }

    private void Refresh()
    {
        var mode = Modes.SelectedIndex;
        PreviewPanel.IsVisible = mode == 0;
        CodePanel.IsVisible = mode > 0;
        if (mode <= 0)
        {
            sourceRevision++;
            SetCopyFeedback();
            return;
        }
        var prepare = prepareSources;
        prepareSources = null;
        prepare?.Invoke();

        bool axaml = mode == 1;
        AxamlKinds.IsVisible = axaml;
        CodeKinds.IsVisible = !axaml;
        var tabs = axaml ? AxamlKinds : CodeKinds;
        CodeTabs.IsVisible = axaml ? tabs.Items.Count > 1 : tabs.Items.Count > 0;
        var language = axaml ? "axaml" : "csharp";
        var raw = (tabs.SelectedItem as ListBoxItem)?.Tag as string ?? (axaml ? AxamlSource : CSharpSource);
        if (SourceCode.Code != raw || SourceCode.Language != language)
        {
            sourceRevision++;
            SetCopyFeedback();
            SourceCode.Language = language;
            SourceCode.Code = raw;
        }
        CopyCodeButton.IsEnabled = !string.IsNullOrEmpty(raw);
    }
}
