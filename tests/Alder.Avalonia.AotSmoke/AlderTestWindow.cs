using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;

namespace Alder.Avalonia.Testing;

internal sealed class AlderTestWindow : AlderWindow
{
    public AlderTestWindow(bool useDialogHost = true)
    {
        ShowTitleBar = false;
        WindowDecorations = WindowDecorations.Full;
        if (useDialogHost) return;

        // Exercise the component's fallback when the host has an overlay but no DialogHost.
        Template = new FuncControlTemplate<AlderTestWindow>((window, scope) => new VisualLayerManager
        {
            Name = "PART_VisualLayerManager",
            Child = new ContentPresenter
            {
                Name = "PART_ContentPresenter",
                [!ContentPresenter.ContentProperty] = window[!ContentProperty]
            }.RegisterInNameScope(scope)
        }.RegisterInNameScope(scope));
    }
}
