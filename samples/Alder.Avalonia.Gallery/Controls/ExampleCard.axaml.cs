using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

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

    private bool ready;
    private Action? loadSources;
    private Action<ExampleCodePanel>? configureCode;
    private ExampleCodePanel? code;
    private bool preparingCode;

    public ExampleCard()
    {
        InitializeComponent();
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

    internal void DeferSources(Action load) => loadSources = load;

    internal void SetSources(string axaml, string viewModel, string codeBehind, string controlUsage)
    {
        configureCode = panel => panel.SetSources(viewModel, codeBehind, controlUsage);
        AxamlSource = axaml;
        CSharpSource = controlUsage;
    }

    internal void ShowAxaml() => Modes.SelectedIndex = 1;

    internal void SetSourceFiles((string Name, string Text)[] axaml, (string Name, string Text)[] csharp)
    {
        configureCode = panel => panel.SetSourceFiles(axaml, csharp);
        AxamlSource = axaml[0].Text;
        CSharpSource = csharp[0].Text;
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

    private void Refresh()
    {
        if (preparingCode) return;
        int mode = Modes.SelectedIndex;
        PreviewPanel.IsVisible = mode == 0;
        CodeHost.IsVisible = mode > 0;
        if (mode <= 0)
        {
            code?.Deactivate();
            return;
        }

        // Load sources and allocate the editor only when a source tab is opened.
        preparingCode = true;
        try
        {
            var load = loadSources;
            loadSources = null;
            load?.Invoke();
            if (code is null)
                CodeHost.Content = code = new ExampleCodePanel();
            configureCode?.Invoke(code);
            configureCode = null;
            code.ShowSource(mode, AxamlSource, CSharpSource);
        }
        finally
        {
            preparingCode = false;
        }
    }
}
