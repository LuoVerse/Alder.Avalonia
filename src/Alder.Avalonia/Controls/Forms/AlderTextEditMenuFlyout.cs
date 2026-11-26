using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace Alder.Avalonia.Controls;

internal enum TextEditMenuMode { Standard, Touch, Selection }

internal sealed class AlderTextEditMenuFlyout : AlderMenuFlyout
{
    private readonly List<IDisposable> bindings = [];
    private AlderMenuItem? cut;
    private AlderMenuItem? copy;
    private AlderMenuItem? paste;

    public TextEditMenuMode Mode { get; init; }

    protected override void OnOpening(CancelEventArgs args)
    {
        base.OnOpening(args);
        if (args.Cancel) return;

        // The shared flyout can reopen on a different control after detaching from its old target.
        if (Target is TextBox editor && Mode != TextEditMenuMode.Selection)
        {
            bool isTouch = Mode == TextEditMenuMode.Touch;
            cut ??= AddItem("AlderText.EditCut", isTouch ? null : TextBox.CutGesture);
            copy ??= AddItem("AlderText.EditCopy", isTouch ? null : TextBox.CopyGesture);
            paste ??= AddItem("AlderText.EditPaste", isTouch ? null : TextBox.PasteGesture);
            BindAction(cut, editor, TextBox.CanCutProperty, editor.Cut, hideWhenDisabled: isTouch);
            BindAction(copy, editor, TextBox.CanCopyProperty, editor.Copy, hideWhenDisabled: isTouch);
            BindAction(paste, editor, TextBox.CanPasteProperty, editor.Paste);
        }
        else if (Target is SelectableTextBlock text && Mode == TextEditMenuMode.Selection)
        {
            copy ??= AddItem("AlderText.EditCopy", TextBox.CopyGesture);
            BindAction(copy, text, SelectableTextBlock.CanCopyProperty, text.Copy);
        }
        else
        {
            args.Cancel = true;
        }
    }

    protected override void OnClosed()
    {
        foreach (var binding in bindings) binding.Dispose();
        bindings.Clear();
        foreach (var item in Items.OfType<AlderMenuItem>()) item.Command = null;
        base.OnClosed();
    }

    private AlderMenuItem AddItem(string headerResource, KeyGesture? gesture)
    {
        var item = new AlderMenuItem { InputGesture = gesture };
        item[!MenuItem.HeaderProperty] = new DynamicResourceExtension(headerResource);
        Items.Add(item);
        return item;
    }

    private void BindAction(AlderMenuItem item, AvaloniaObject owner, AvaloniaProperty<bool> capability,
        Action execute, bool hideWhenDisabled = false)
    {
        item.Command = new ClipboardCommand(execute);
        var available = owner.GetObservable(capability);
        bindings.Add(item.Bind(InputElement.IsEnabledProperty, available));
        if (hideWhenDisabled) bindings.Add(item.Bind(Visual.IsVisibleProperty, available));
    }

    private sealed class ClipboardCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }
}
