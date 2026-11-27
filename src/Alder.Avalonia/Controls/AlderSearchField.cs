using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;

namespace Alder.Avalonia.Controls;

/// <summary>Adds search submission and optional clearing affordances to a text field.</summary>
public sealed class AlderSearchField : AlderInput
{
    /// <summary>Identifies the <see cref="SubmitCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> SubmitCommandProperty =
        AvaloniaProperty.Register<AlderSearchField, ICommand?>(nameof(SubmitCommand));
    /// <summary>Identifies the <see cref="SearchIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> SearchIconProperty =
        AvaloniaProperty.Register<AlderSearchField, Geometry?>(nameof(SearchIcon));
    /// <summary>Identifies the <see cref="ClearIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> ClearIconProperty =
        AvaloniaProperty.Register<AlderSearchField, Geometry?>(nameof(ClearIcon));
    /// <summary>Identifies the <see cref="AlderInput.ShowClearButton"/> Avalonia property.</summary>
    public new static readonly StyledProperty<bool> ShowClearButtonProperty =
        AlderInput.ShowClearButtonProperty.AddOwner<AlderSearchField>(new StyledPropertyMetadata<bool>(true));
    internal static readonly StyledProperty<ControlTheme?> ClearButtonThemeProperty =
        AvaloniaProperty.Register<AlderSearchField, ControlTheme?>(nameof(ClearButtonTheme));

    /// <summary>The command executed when the current search text is submitted.</summary>
    public ICommand? SubmitCommand { get => GetValue(SubmitCommandProperty); set => SetValue(SubmitCommandProperty, value); }
    /// <summary>The geometry used for the search icon.</summary>
    public Geometry? SearchIcon { get => GetValue(SearchIconProperty); set => SetValue(SearchIconProperty, value); }
    /// <summary>The geometry used for the clear icon.</summary>
    public Geometry? ClearIcon { get => GetValue(ClearIconProperty); set => SetValue(ClearIconProperty, value); }
    internal override bool UsesTemplateClearButton => false;
    internal ControlTheme? ClearButtonTheme { get => GetValue(ClearButtonThemeProperty); set => SetValue(ClearButtonThemeProperty, value); }
    /// <summary>Raised after the user clears the search text.</summary>
    public event EventHandler? Cleared;

    private readonly Button clearButton;
    private TextPresenter? presenter;

    /// <summary>Initializes a new instance of <see cref="AlderSearchField"/>.</summary>
    public AlderSearchField()
    {
        var icon = new AlderPathIcon { Width = 16, Height = 16, IsHitTestVisible = false };
        icon[!PathIcon.DataProperty] = this[!SearchIconProperty];
        icon[!ForegroundProperty] = new DynamicResourceExtension("AlderMuted");
        SetValue(InnerLeftContentProperty, icon, BindingPriority.Style);

        var clearIcon = new AlderPathIcon { Width = 12, Height = 12, IsHitTestVisible = false };
        clearIcon[!PathIcon.DataProperty] = this[!ClearIconProperty];
        clearButton = new AlderButton { Width = 20, Height = 20, MinHeight = 20, Content = clearIcon, IsVisible = false };
        clearButton[!ThemeProperty] = this[!ClearButtonThemeProperty];
        clearButton[!AutomationProperties.NameProperty] = new DynamicResourceExtension("AlderText.ClearSearch");
        clearButton.Click += (_, _) => ClearQuery();
        SetValue(InnerRightContentProperty, clearButton, BindingPriority.Style);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        presenter = e.NameScope.Find<TextPresenter>("PART_TextPresenter");
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (IsFocused && !e.Handled && e.KeyModifiers == KeyModifiers.None && string.IsNullOrEmpty(presenter?.PreeditText))
        {
            if (e.Key == Key.Escape && ClearQuery())
            {
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Enter && SubmitCommand is { } command)
            {
                var query = Text ?? "";
                e.Handled = true;
                if (!IsInvalid && !DataValidationErrors.GetHasErrors(this) && command.CanExecute(query))
                    command.Execute(query);
                return;
            }
        }
        base.OnKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (clearButton is not null && (change.Property == TextProperty || change.Property == IsReadOnlyProperty ||
                                        change.Property == ShowClearButtonProperty))
        {
            bool show = ShowClearButton && !IsReadOnly && !string.IsNullOrEmpty(Text);
            if (!show && clearButton.IsFocused) Focus();
            clearButton.IsVisible = show;
        }
    }

    private bool ClearQuery()
    {
        if (!IsEffectivelyEnabled || IsReadOnly || string.IsNullOrEmpty(Text)) return false;
        Focus();
        SetCurrentValue(TextProperty, "");
        Cleared?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
