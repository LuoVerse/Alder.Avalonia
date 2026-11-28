using System.Collections;
using System.ComponentModel;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class FormCompositionInteractions
{
    [AvaloniaFact]
    public void TextFieldSharesTextAccessibilityAndBindingErrorsWithItsNativeEditor()
    {
        var model = new ValidatingText();
        var field = new AlderTextField { Label = "Project name", Description = "Visible to teammates.", IsRequired = true };
        field.Bind(AlderTextField.TextProperty, CompiledBinding.Create((ValidatingText vm) => vm.Text, source: model));
        var window = Show(field);
        try
        {
            var editor = Editor(field);
            var label = field.GetVisualDescendants().OfType<AlderLabel>().Single();
            var description = field.GetVisualDescendants().OfType<AlderDescription>().Single();
            var error = field.GetVisualDescendants().OfType<AlderFieldError>().Single();
            Assert.Same(editor, label.Target);
            Assert.Same(label, AutomationProperties.GetLabeledBy(editor));
            Assert.True(AutomationProperties.GetIsRequiredForForm(editor));
            Assert.Equal(field.Description, AutomationProperties.GetHelpText(editor));
            Assert.False(error.IsVisible);

            Assert.True(field.FocusEditor());
            window.KeyTextInput("项目");
            Pump(window);
            Assert.Equal("项目", model.Text);
            model.Text = "Library";
            Assert.Equal("Library", editor.Text);

            model.ReportErrors("Name is reserved.", "Choose a different name.");
            Pump(window);
            Assert.True(field.HasValidationError);
            Assert.True(editor.IsInvalid);
            Assert.True(error.IsVisible);
            Assert.Equal("Name is reserved." + Environment.NewLine + "Choose a different name.", error.Text);
            Assert.Equal(error.Text, AutomationProperties.GetHelpText(editor));
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(error));
            Assert.False(description.IsVisible);

            field.ResetValidation();
            Assert.True(field.HasValidationError);
            model.ReportErrors();
            Pump(window);
            Assert.False(field.HasValidationError);
            Assert.False(error.IsVisible);
            Assert.True(description.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SubmitValidatesEnabledFieldsOpensDisclosureAndPreservesMultilineEditing()
    {
        var name = new AlderTextField { Label = "Name", IsRequired = true };
        var notes = new AlderTextField { Label = "Notes", AcceptsReturn = true, IsRequired = true };
        var collapsed = new AlderDisclosure { Header = "Details", Content = notes, IsExpanded = false };
        var ignored = new AlderFieldset { IsEnabled = false, Content = new AlderTextField { IsRequired = true } };
        var hidden = new AlderTextField { IsRequired = true, IsVisible = false };
        var nested = new AlderForm { Content = new AlderTextField { IsRequired = true } };
        var save = new AlderButton { Content = "Save" };
        var saveCommand = new TestCommand();
        var form = new AlderForm
        {
            Content = new AlderFieldset
            {
                Content = new StackPanel { Children = { name, ignored, hidden, collapsed, nested } }, Actions = save
            },
            SubmitCommand = saveCommand, SubmitCommandParameter = "project"
        };
        save.Command = form.Submit;
        var window = Show(form);
        try
        {
            Click(window, save);
            Assert.True(Editor(name).IsFocused);
            Assert.True(name.HasValidationError);
            Assert.True(notes.HasValidationError);
            Assert.False(collapsed.IsExpanded);
            window.KeyTextInput("Library");
            Pump(window);
            Assert.False(name.HasValidationError);

            Click(window, save);
            Assert.True(collapsed.IsExpanded);
            Assert.True(Editor(notes).IsFocused);
            Assert.True(Editor(notes).Bounds.Height > Editor(name).Bounds.Height);
            window.KeyTextInput("First line");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyTextInput("Second line");
            Pump(window);
            Assert.Equal("First line\nSecond line", notes.Text?.ReplaceLineEndings("\n"));
            Assert.Empty(saveCommand.Parameters);

            Assert.True(save.Focus());
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Pump(window);
            Assert.Equal("project", Assert.Single(saveCommand.Parameters));

            form.IsSubmitting = true;
            Assert.False(save.IsEffectivelyEnabled);
            Assert.False(form.TrySubmit());
            form.IsSubmitting = false;
            saveCommand.Allowed = false;
            Assert.False(save.IsEffectivelyEnabled);
            saveCommand.Allowed = true;
            Assert.True(save.IsEffectivelyEnabled);
            name.Text = "";
            Assert.True(name.HasValidationError);
            form.ResetValidation();
            Assert.False(name.HasValidationError);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void StandaloneFieldErrorTracksTargetAndFormFocusSkipsItsLabel()
    {
        var input = new AlderInput();
        var label = new AlderLabel { Content = "Label", Target = input };
        var error = new AlderFieldError { Target = input };
        var save = new TestCommand();
        var form = new AlderForm { Content = new StackPanel { Children = { label, input, error } }, SubmitCommand = save };
        var window = Show(form);
        try
        {
            Assert.False(error.IsVisible);
            DataValidationErrors.SetErrors(input, ["First error", new InvalidOperationException("Second error")]);
            Pump(window);
            Assert.True(error.IsVisible);
            Assert.Equal("First error" + Environment.NewLine + "Second error", error.Text);
            Assert.False(form.TrySubmit());
            Assert.True(input.IsFocused);

            error.Text = "Custom error";
            Assert.True(error.IsVisible);
            DataValidationErrors.ClearErrors(input);
            Pump(window);
            Assert.False(error.IsVisible);
            Assert.True(form.TrySubmit());

            input.IsInvalid = true;
            Assert.True(error.IsVisible);
            error.Target = new AlderInput();
            Assert.False(error.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void FieldsetNamesTheGroupAndDisablesItsFieldsAndActions()
    {
        var input = new AlderTextField { Label = "Name" };
        var action = new AlderButton { Content = "Save" };
        var group = new AlderFieldset { Legend = "Profile", Description = "Shared with the team.", Content = input, Actions = action };
        var window = Show(group);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(group);
            Assert.Equal(AutomationControlType.Group, peer.GetAutomationControlType());
            Assert.Equal(group.Legend, peer.GetName());
            Assert.Equal(group.Description, peer.GetHelpText());
            Assert.True(action.IsEffectivelyVisible);
            group.IsEnabled = false;
            Assert.False(Editor(input).IsEffectivelyEnabled);
            Assert.False(action.IsEffectivelyEnabled);
            group.IsEnabled = true;
            Assert.True(Editor(input).IsEffectivelyEnabled);
            Assert.True(action.IsEffectivelyEnabled);
        }
        finally { window.Close(); }
    }

    private static AlderInput Editor(AlderTextField field) => field.GetVisualDescendants().OfType<AlderInput>().Single();

    private static Window Show(Control content)
    {
        var window = new AlderTestWindow { Width = 430, Height = 480, Content = content };
        AlderMotion.SetReduced(window, true);
        window.Show();
        Pump(window);
        return window;
    }

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump(window);
    }

    private static void Pump(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class ValidatingText : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private string[] errors = [];
        public string? Text
        {
            get => field;
            set
            {
                if (field == value) return;
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            }
        }
        public bool HasErrors => errors.Length != 0;
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
        public IEnumerable GetErrors(string? propertyName) => errors;
        public void ReportErrors(params string[] next)
        {
            errors = next;
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Text)));
        }
    }

    private sealed class TestCommand : ICommand
    {
        public bool Allowed
        {
            get => field;
            set
            {
                field = value;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        } = true;
        public List<object?> Parameters { get; } = [];
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => Allowed;
        public void Execute(object? parameter) => Parameters.Add(parameter);
    }
}
