using System.Runtime.InteropServices;
using Alder.Avalonia.Controls;
using Alder.NativeMarkdown.UI.Extensions;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace Alder.Avalonia.Gallery.Controls;

public partial class ExampleCodePanel : UserControl
{
    private static readonly MarkdownServices SourceServices = new()
    {
        Highlighter = AlderMarkdown.ChatServices.Highlighter,
        CodeBlocks = AlderMarkdown.ChatServices.CodeBlocks with { ShowHeader = false, ShowLineNumbers = true }
    };

    private int mode;
    private int sourceRevision;
    private string axamlSource = "";
    private string csharpSource = "";

    public ExampleCodePanel()
    {
        InitializeComponent();
        MarkdownServices.SetServices(SourceCode, SourceServices);
        SourceCode.AddHandler(RequestBringIntoViewEvent, KeepCodeFocusInPlace);
    }

    private void KeepCodeFocusInPlace(object? sender, RequestBringIntoViewEventArgs e)
    {
        // Pointer focus can ask the page to scroll the entire source block into view during a scrollbar drag.
        if (e.TargetObject == SourceCode && e.TargetRect.Size == SourceCode.Bounds.Size &&
            !SourceCode.Classes.Contains(":focus-visible"))
            e.Handled = true;
    }

    internal void SetSources(string viewModel, string codeBehind, string controlUsage)
    {
        AddCodeKind("ViewModelSource", viewModel);
        AddCodeKind("CodeBehindSource", codeBehind);
        AddCodeKind("ControlUsageSource", controlUsage);
        CodeKinds.SelectedIndex = 0;
    }

    internal void SetSourceFiles((string Name, string Text)[] axaml, (string Name, string Text)[] csharp)
    {
        SetFiles(AxamlKinds, axaml);
        SetFiles(CodeKinds, csharp);
    }

    internal void ShowSource(int selectedMode, string axaml, string csharp)
    {
        mode = selectedMode;
        axamlSource = axaml;
        csharpSource = csharp;
        Refresh();
    }

    internal void Deactivate()
    {
        if (mode == 0) return;
        mode = 0;
        sourceRevision++;
        SetCopyFeedback();
    }

    private void SourceChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (mode > 0) Refresh();
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

    private async void CopyCode(object? sender, RoutedEventArgs e)
    {
        var code = SourceCode.Code;
        if (mode <= 0 || string.IsNullOrEmpty(code)) return;
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
        if (sourceRevision == revision && IsEffectivelyVisible)
            SetCopyFeedback(copied);
    }

    private void SetCopyFeedback(bool? copied = null)
    {
        CopyCodeButton.Classes.Set("copied", copied == true);
        CopyCodeButton.Classes.Set("copyFailed", copied == false);
    }

    private void Refresh()
    {
        bool axaml = mode == 1;
        AxamlKinds.IsVisible = axaml;
        CodeKinds.IsVisible = !axaml;
        var tabs = axaml ? AxamlKinds : CodeKinds;
        CodeTabs.IsVisible = axaml ? tabs.Items.Count > 1 : tabs.Items.Count > 0;
        var language = axaml ? "axaml" : "csharp";
        var raw = (tabs.SelectedItem as ListBoxItem)?.Tag as string ?? (axaml ? axamlSource : csharpSource);
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
