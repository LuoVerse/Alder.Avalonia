using System.Collections;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Specifies which interactions automatically open the options list.</summary>
public enum PickerMenuTrigger
{
    /// <summary>Opens the options list when the editor gains focus.</summary>
    Focus,
    /// <summary>Opens the options list in response to editing.</summary>
    Input,
    /// <summary>Requires explicit activation to open the options list.</summary>
    Manual
}

/// <summary>An optional item model for labelled, described, grouped or disabled options.</summary>
public class AlderPickerOption
{
    /// <summary>The display label of this option.</summary>
    public string Text { get; set; } = string.Empty;
    /// <summary>Compact caption for a trigger that also displays provider or category information.</summary>
    public string? ShortText { get; set; }
    /// <summary>Optional secondary text beneath the option label.</summary>
    public string? Description { get; set; }
    /// <summary>An optional heading used to group the option with related items.</summary>
    public string? Group { get; set; }
    /// <summary>Optional leading media for this option.</summary>
    public IImage? Image { get; set; }
    /// <summary>Whether the option can be selected.</summary>
    public bool IsEnabled { get; set; } = true;
    /// <inheritdoc />
    public override string ToString() => Text;
}

/// <summary>Common native selection and popup behavior for the Alder pickers.</summary>
public abstract class AlderPicker : SelectingItemsControl
{
    internal const double ListBoxPopupGap = 8;
    /// <summary>Identifies the <see cref="SelectedItems"/> Avalonia property.</summary>
    public new static readonly DirectProperty<SelectingItemsControl, IList?> SelectedItemsProperty =
        SelectingItemsControl.SelectedItemsProperty;
    /// <summary>Identifies the <see cref="Label"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<AlderPicker, string?>(nameof(Label));
    /// <summary>Identifies the <see cref="SelectionPlaceholderText"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> SelectionPlaceholderTextProperty =
        AvaloniaProperty.Register<AlderPicker, string?>(nameof(SelectionPlaceholderText));
    /// <summary>Identifies the <see cref="Description"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<AlderPicker, string?>(nameof(Description));
    /// <summary>Identifies the <see cref="ErrorText"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty = AvaloniaProperty.Register<AlderPicker, string?>(nameof(ErrorText));
    /// <summary>Identifies the <see cref="PlaceholderText"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        TextBox.PlaceholderTextProperty.AddOwner<AlderPicker>();
    /// <summary>Identifies the <see cref="SearchPlaceholderText"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> SearchPlaceholderTextProperty =
        AvaloniaProperty.Register<AlderPicker, string?>(nameof(SearchPlaceholderText));
    /// <summary>Identifies the <see cref="EmptyText"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> EmptyTextProperty = AvaloniaProperty.Register<AlderPicker, string?>(nameof(EmptyText));
    /// <summary>Identifies the <see cref="LoadingText"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> LoadingTextProperty = AvaloniaProperty.Register<AlderPicker, string?>(nameof(LoadingText));
    /// <summary>Identifies the <see cref="Text"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AlderPicker, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<InputVariant> VariantProperty = AlderInput.VariantProperty.AddOwner<AlderPicker>();
    /// <summary>Identifies the <see cref="FullWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> FullWidthProperty = AlderInput.FullWidthProperty.AddOwner<AlderPicker>();
    /// <summary>Identifies the <see cref="IsRequired"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty = AlderInput.IsRequiredProperty.AddOwner<AlderPicker>();
    /// <summary>Identifies the <see cref="IsInvalid"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty = AlderInput.IsInvalidProperty.AddOwner<AlderPicker>();
    /// <summary>Identifies the <see cref="IsReadOnly"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = TextBox.IsReadOnlyProperty.AddOwner<AlderPicker>();
    /// <summary>Identifies the <see cref="IsLoading"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<AlderPicker, bool>(nameof(IsLoading));
    /// <summary>Identifies the <see cref="ShowClearButton"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowClearButtonProperty = AvaloniaProperty.Register<AlderPicker, bool>(nameof(ShowClearButton));
    /// <summary>Identifies the <see cref="AllowsEmptyCollection"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> AllowsEmptyCollectionProperty =
        AvaloniaProperty.Register<AlderPicker, bool>(nameof(AllowsEmptyCollection));
    /// <summary>Identifies the <see cref="AllowsCustomValue"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> AllowsCustomValueProperty =
        AvaloniaProperty.Register<AlderPicker, bool>(nameof(AllowsCustomValue));
    /// <summary>Identifies the <see cref="IsDropDownOpen"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsDropDownOpenProperty =
        AvaloniaProperty.Register<AlderPicker, bool>(nameof(IsDropDownOpen), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="MenuTrigger"/> Avalonia property.</summary>
    public static readonly StyledProperty<PickerMenuTrigger> MenuTriggerProperty =
        AvaloniaProperty.Register<AlderPicker, PickerMenuTrigger>(nameof(MenuTrigger));
    /// <summary>Identifies the <see cref="MaxDropDownHeight"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MaxDropDownHeightProperty =
        AvaloniaProperty.Register<AlderPicker, double>(nameof(MaxDropDownHeight), 280, validate: x => x > 0);
    /// <summary>Identifies the <see cref="Indicator"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> IndicatorProperty = AvaloniaProperty.Register<AlderPicker, Geometry?>(nameof(Indicator));
    /// <summary>Identifies the <see cref="SelectionTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> SelectionTemplateProperty =
        AvaloniaProperty.Register<AlderPicker, IDataTemplate?>(nameof(SelectionTemplate));
    /// <summary>Identifies the <see cref="TextSelector"/> Avalonia property.</summary>
    public static readonly StyledProperty<Func<object?, string?>?> TextSelectorProperty =
        AvaloniaProperty.Register<AlderPicker, Func<object?, string?>?>(nameof(TextSelector));
    /// <summary>Identifies the <see cref="Filter"/> Avalonia property.</summary>
    public static readonly StyledProperty<Func<string, object?, bool>?> FilterProperty =
        AvaloniaProperty.Register<AlderPicker, Func<string, object?, bool>?>(nameof(Filter));
    /// <summary>Identifies the <see cref="SelectionText"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderPicker, string> SelectionTextProperty =
        AvaloniaProperty.RegisterDirect<AlderPicker, string>(nameof(SelectionText), picker => picker.SelectionText);
    /// <summary>Identifies the <see cref="IsEditable"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderPicker, bool> IsEditableProperty =
        AvaloniaProperty.RegisterDirect<AlderPicker, bool>(nameof(IsEditable), picker => picker.IsEditable);

    /// <summary>The placeholder shown when there is no selected value.</summary>
    public string? SelectionPlaceholderText { get => GetValue(SelectionPlaceholderTextProperty); set => SetValue(SelectionPlaceholderTextProperty, value); }
    /// <summary>The text that identifies the control or value.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>The optional supporting text displayed with the content.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>The text shown when the field is marked invalid.</summary>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }
    /// <summary>The placeholder for the main input when it is empty.</summary>
    public string? PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }
    /// <summary>The placeholder for the drop-down's search box.</summary>
    public string? SearchPlaceholderText { get => GetValue(SearchPlaceholderTextProperty); set => SetValue(SearchPlaceholderTextProperty, value); }
    /// <summary>The message displayed when there are no options to show.</summary>
    public string? EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }
    /// <summary>The message displayed while options are loading.</summary>
    public string? LoadingText { get => GetValue(LoadingTextProperty); set => SetValue(LoadingTextProperty, value); }
    /// <summary>The editable text used by text-entry pickers and filters.</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    /// <summary>The visual treatment applied to the control.</summary>
    public InputVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>Whether the control stretches to fill the available width.</summary>
    public bool FullWidth { get => GetValue(FullWidthProperty); set => SetValue(FullWidthProperty, value); }
    /// <summary>Whether the field is presented as required.</summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }
    /// <summary>Whether the control presents its invalid state.</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }
    /// <summary>Whether interactions can change the picker value.</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    /// <summary>Whether the picker is waiting for options to load.</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
    /// <summary>Whether the picker exposes a way to clear its current value.</summary>
    public bool ShowClearButton { get => GetValue(ShowClearButtonProperty); set => SetValue(ShowClearButtonProperty, value); }
    /// <summary>Whether the drop-down can open with no available options.</summary>
    public bool AllowsEmptyCollection { get => GetValue(AllowsEmptyCollectionProperty); set => SetValue(AllowsEmptyCollectionProperty, value); }
    /// <summary>Whether an editable picker accepts text outside the supplied options.</summary>
    public bool AllowsCustomValue { get => GetValue(AllowsCustomValueProperty); set => SetValue(AllowsCustomValueProperty, value); }
    /// <summary>Whether the options popup is requested open.</summary>
    /// <remarks>The picker updates this property when closing through a selection, Escape, or light dismissal.</remarks>
    public bool IsDropDownOpen { get => GetValue(IsDropDownOpenProperty); set => SetValue(IsDropDownOpenProperty, value); }
    /// <summary>The interaction that automatically opens the options popup.</summary>
    public PickerMenuTrigger MenuTrigger { get => GetValue(MenuTriggerProperty); set => SetValue(MenuTriggerProperty, value); }
    /// <summary>The maximum height of the options popup in device-independent pixels.</summary>
    public double MaxDropDownHeight { get => GetValue(MaxDropDownHeightProperty); set => SetValue(MaxDropDownHeightProperty, value); }
    /// <summary>The optional indicator displayed alongside the main content.</summary>
    public Geometry? Indicator { get => GetValue(IndicatorProperty); set => SetValue(IndicatorProperty, value); }
    /// <summary>The template used for displaying the selected value in the field.</summary>
    public IDataTemplate? SelectionTemplate { get => GetValue(SelectionTemplateProperty); set => SetValue(SelectionTemplateProperty, value); }
    /// <summary>A function that extracts the display and search text from an item.</summary>
    public Func<object?, string?>? TextSelector { get => GetValue(TextSelectorProperty); set => SetValue(TextSelectorProperty, value); }
    /// <summary>The predicate used to decide whether an item matches the active filter.</summary>
    public Func<string, object?, bool>? Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }
    /// <summary>Whether the picker permits one selection or multiple selections.</summary>
    public new SelectionMode SelectionMode { get => base.SelectionMode; set => base.SelectionMode = value; }
    /// <summary>The items in the current picker selection.</summary>
    public new IList? SelectedItems { get => base.SelectedItems; set => base.SelectedItems = value; }
    /// <summary>The text representation of the current picker selection.</summary>
    public string SelectionText { get => field; private set => SetAndRaise(SelectionTextProperty, ref field, value); } = string.Empty;
    /// <summary>Whether this picker has an editable text field.</summary>
    public bool IsEditable => this is AlderComboBox;

    private Control? anchor;
    private Button? trigger;
    private Button? toggle;
    private Button? clear;
    private AlderInput? editor;
    private WrapPanel? selectionTags;
    private AlderSearchField? search;
    private PickerList? list;
    private StackPanel? panel;
    private TextBlock? status;
    private AlderPopover? flyout;
    private List<PickerEntry> rows = [];
    private string rowsQuery = string.Empty;
    private bool settingText;
    private bool changingFocus;
    private bool closing;
    private bool showUnfilteredItems;
    private bool keepDraftOnClose;
    private string? committedCustomText;
    private bool templateChanging;
    private long lastTypedAt;
    private string typedPrefix = string.Empty;
    private bool IsMultiple => SelectionMode.HasFlag(SelectionMode.Multiple);
    private bool HasSearch => this is AlderAutocomplete or AlderModelPicker { ShowSearch: true };
    private bool IsSelect => this is AlderSelect;
    private bool UsesListBoxPopup => IsSelect || IsEditable;

    /// <summary>Initializes a new instance of <see cref="AlderPicker"/>.</summary>
    protected AlderPicker()
    {
        PseudoClasses.Set(":autocomplete", this is AlderAutocomplete);
        PseudoClasses.Set(":select", IsSelect);
        SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, this)) return;
            UpdateSelection(syncText: !settingText);
            if (IsDropDownOpen && list is not null)
                foreach (var container in list.GetRealizedContainers())
                    if (container is ListBoxItem { Content: PickerRow { IsOption: true } row })
                        row.IsCommitted = Selection.IsSelected(row.SourceIndex);
        };
        ItemsView.CollectionChanged += (_, _) => { if (IsDropDownOpen) RefreshRows(); };
        AddHandler(KeyDownEvent, HandleKey, RoutingStrategies.Tunnel);
        if (this is AlderSelect) AddHandler(TextInputEvent, HandleTypeAhead, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        templateChanging = true;
        try { ClosePopup(); }
        finally { templateChanging = false; }
        if (anchor is not null) anchor.PropertyChanged -= OnFieldChanged;
        if (trigger is not null) trigger.Click -= TogglePopup;
        if (toggle is not null) toggle.Click -= TogglePopup;
        if (clear is not null) clear.Click -= ClearSelection;
        if (editor is not null)
        {
            editor.GotFocus -= OnEditorFocused;
            editor.LostFocus -= OnEditorLostFocus;
            editor.RemoveHandler(PointerPressedEvent, OnEditorPressed);
        }
        base.OnApplyTemplate(e);
        anchor = e.NameScope.Find<Control>("PART_Field");
        if (anchor is not null) anchor.PropertyChanged += OnFieldChanged;
        trigger = e.NameScope.Find<Button>("PART_Trigger");
        toggle = e.NameScope.Find<Button>("PART_Toggle");
        clear = e.NameScope.Find<Button>("PART_Clear");
        editor = e.NameScope.Find<AlderInput>("PART_Editor");
        selectionTags = HasSearch ? e.NameScope.Find<WrapPanel>("PART_SelectionTags") : null;
        if (trigger is not null) trigger.Click += TogglePopup;
        if (toggle is not null) toggle.Click += TogglePopup;
        if (clear is not null) clear.Click += ClearSelection;
        if (editor is not null)
        {
            editor.GotFocus += OnEditorFocused;
            editor.LostFocus += OnEditorLostFocus;
            editor.AddHandler(PointerPressedEvent, OnEditorPressed, RoutingStrategies.Tunnel);
        }
        UpdateSelection(syncText: SelectedIndex >= 0);
        if (IsDropDownOpen) OpenPopup();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (IsDropDownOpen) OpenPopup();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        SetCurrentValue(IsDropDownOpenProperty, false);
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsDropDownOpenProperty)
        {
            PseudoClasses.Set(":open", IsDropDownOpen);
            if (IsDropDownOpen) OpenPopup();
            else ClosePopup();
        }
        else if (change.Property == TextProperty && !settingText)
        {
            showUnfilteredItems = false;
            if (search is not null && search.Text != Text) search.SetCurrentValue(TextBox.TextProperty, Text);
            if (IsDropDownOpen) RefreshRows();
            else if (IsEditable && editor?.IsFocused == true && MenuTrigger != PickerMenuTrigger.Manual && !changingFocus)
                SetCurrentValue(IsDropDownOpenProperty, true);
        }
        else if (change.Property == ItemsSourceProperty || change.Property == ItemTemplateProperty ||
                 change.Property == TextSelectorProperty || change.Property == FilterProperty ||
                 change.Property == IsLoadingProperty || change.Property == EmptyTextProperty ||
                 change.Property == LoadingTextProperty || change.Property == MaxDropDownHeightProperty)
        {
            if (change.Property == TextSelectorProperty || change.Property == ItemsSourceProperty)
                UpdateSelection(syncText: SelectedIndex >= 0);
            if (IsDropDownOpen) RefreshRows();
        }
        else if (change.Property == SelectionModeProperty || change.Property == ShowClearButtonProperty ||
                 change.Property == SelectionTemplateProperty || change.Property == IsReadOnlyProperty ||
                 change.Property == AlderAutocomplete.ShowSelectionTagsProperty || change.Property == VariantProperty ||
                 change.Property == SelectionPlaceholderTextProperty)
        {
            UpdateSelection(syncText: false);
            if (IsReadOnly) SetCurrentValue(IsDropDownOpenProperty, false);
        }
        else if (change.Property == DescriptionProperty || change.Property == ErrorTextProperty || change.Property == IsInvalidProperty)
        {
            PseudoClasses.Set(":has-description", !string.IsNullOrEmpty(Description) && (!UsesListBoxPopup || !IsInvalid));
            PseudoClasses.Set(":field-error", IsInvalid && !string.IsNullOrEmpty(ErrorText));
        }
        else if (change.Property == AlderSelect.DropDownPaddingProperty ||
                 change.Property == AlderSelect.DropDownBorderThicknessProperty ||
                 change.Property == AlderSelect.DropDownMinWidthProperty ||
                 change.Property == AlderInlineSelect.DropDownWidthProperty)
        {
            if (IsDropDownOpen) SizePopup();
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
            SetCurrentValue(IsDropDownOpenProperty, false);
    }

    private void OnFieldChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty && IsDropDownOpen)
            SizePopup();
    }

    private void OnEditorFocused(object? sender, FocusChangedEventArgs e)
    {
        if (AllowsCustomValue && SelectedIndex < 0 && !changingFocus && !IsDropDownOpen)
            committedCustomText = Text;
        OpenOnEditorInteraction(sender);
    }

    private void OnEditorLostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (changingFocus || !ReferenceEquals(e.Source, editor)) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(sender, editor) || editor?.IsFocused == true ||
                panel?.IsKeyboardFocusWithin == true || !this.IsAttachedToVisualTree()) return;
            if (IsDropDownOpen) Dismiss(restoreFocus: false);
            else if (!IsMultiple && !IsReadOnly) FinishEditing();
        }, DispatcherPriority.Input);
    }

    private void OnEditorPressed(object? sender, PointerPressedEventArgs e)
    {
        // Light dismiss can leave the editor focused, so another click may not raise GotFocus.
        if (editor?.IsFocused == true && e.GetCurrentPoint(editor).Properties.IsLeftButtonPressed)
            OpenOnEditorInteraction(sender);
    }

    private void OpenOnEditorInteraction(object? sender)
    {
        if (IsDropDownOpen || IsReadOnly || MenuTrigger != PickerMenuTrigger.Focus || changingFocus) return;
        // Opening an overlay can change the native focus scope. Let the current
        // focus operation finish before the overlay is attached.
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(sender, editor) && editor?.IsFocused == true && this.IsAttachedToVisualTree() &&
                MenuTrigger == PickerMenuTrigger.Focus && !IsDropDownOpen)
            {
                showUnfilteredItems = true;
                SetCurrentValue(IsDropDownOpenProperty, true);
            }
        }, DispatcherPriority.Input);
    }

    private void TogglePopup(object? sender, RoutedEventArgs e)
    {
        if (IsReadOnly) return;
        if (IsDropDownOpen) Dismiss(restoreFocus: true);
        else
        {
            if (IsEditable) showUnfilteredItems = true;
            SetCurrentValue(IsDropDownOpenProperty, true);
        }
    }

    private void ClearSelection(object? sender, RoutedEventArgs e) =>
        ClearSelection(GetFocusMethod(sender as Control));

    private static NavigationMethod GetFocusMethod(Control? control) =>
        control?.Classes.Contains(":focus-visible") == true ? NavigationMethod.Tab : NavigationMethod.Pointer;

    private void ClearSelection(NavigationMethod focusMethod = NavigationMethod.Tab)
    {
        if (IsReadOnly) return;
        Selection.Clear();
        WriteText(null);
        committedCustomText = null;
        if (!IsSelect && IsDropDownOpen) RefreshRows();
        FocusField(focusMethod);
    }

    private string GetText(object? item) => TextSelector?.Invoke(item) ?? item?.ToString() ?? string.Empty;

    private void UpdateSelection(bool syncText)
    {
        var selected = Selection.SelectedItems;
        var captions = selected.Select(GetText).ToArray();
        SelectionText = UsesListBoxPopup ? JoinSelectValues(captions) :
            string.Join(CultureInfo.CurrentCulture.TextInfo.ListSeparator + " ", captions);
        PseudoClasses.Set(":has-selection", selected.Count > 0);
        PseudoClasses.Set(":selection-template", !IsMultiple && SelectedIndex >= 0 && SelectionTemplate is not null);
        PseudoClasses.Set(":multiple", IsMultiple);
        PseudoClasses.Set(":selection-placeholder", IsEditable && IsMultiple && selected.Count == 0 &&
            !string.IsNullOrEmpty(SelectionPlaceholderText));
        PseudoClasses.Set(":clearable", ShowClearButton && selected.Count > 0 && !IsReadOnly);
        UpdateSelectionTags();
        if (syncText && IsEditable && !IsMultiple)
            WriteText(SelectedIndex >= 0 ? GetText(SelectedItem) : null);
    }

    private string JoinSelectValues(string[] captions)
    {
        if (captions.Length <= 1) return captions.FirstOrDefault() ?? "";
        var resource = captions.Length == 2 ? "AlderText.SelectPair" : "AlderText.SelectListEnd";
        var separator = this.TryFindResource("AlderText.SelectSeparator", out var delimiter) && delimiter is string text
            ? text : CultureInfo.CurrentCulture.TextInfo.ListSeparator + " ";
        return this.TryFindResource(resource, out var format) && format is string pattern
            ? string.Format(CultureInfo.CurrentCulture, pattern, string.Join(separator, captions, 0, captions.Length - 1), captions[^1])
            : string.Join(separator, captions);
    }

    private void UpdateSelectionTags()
    {
        if (selectionTags is null) return;
        bool show = IsMultiple && Selection.SelectedItems.Count > 0 &&
                    this is AlderAutocomplete { ShowSelectionTags: true };
        var focusedTag = selectionTags.IsKeyboardFocusWithin
            ? selectionTags.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.IsFocused)
            : null;
        var focusMethod = GetFocusMethod(focusedTag);
        PseudoClasses.Set(":selection-tags", show);
        selectionTags.Children.Clear();
        if (show)
        {
            foreach (int index in Selection.SelectedIndexes)
                selectionTags.Children.Add(CreateSelectionTag(GetText(ItemsView[index]), index));
        }
        if (focusedTag is not null &&
            selectionTags.GetVisualDescendants().OfType<Button>().FirstOrDefault()?.Focus(focusMethod) != true)
            FocusField(focusMethod);
    }

    private Border CreateSelectionTag(string text, int index)
    {
        var label = new AlderTextBlock
        {
            Text = text, FontSize = 12, LineHeight = 16, TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        var contents = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { label } };
        if (!IsReadOnly)
        {
            var icon = new AlderPathIcon { Width = 8, Height = 8, IsHitTestVisible = false };
            icon[!PathIcon.DataProperty] = new DynamicResourceExtension("AlderIconXmark");
            icon[!ForegroundProperty] = new DynamicResourceExtension("AlderInk");
            var remove = new TagRemoveButton { Content = icon, Width = 12, Height = 12, CornerRadius = new CornerRadius(6) };
            remove[!ThemeProperty] = new DynamicResourceExtension("AlderPickerTagRemoveTheme");
            AutomationProperties.SetName(remove, text);
            remove[!AutomationProperties.HelpTextProperty] = new DynamicResourceExtension("AlderText.PickerRemove");
            remove.Click += (_, e) =>
            {
                e.Handled = true;
                if (IsReadOnly) return;
                // The pointer can activate a tag that already has visible keyboard focus.
                // Move focus before the tag tree is rebuilt, so no sibling inherits that focus.
                if (remove.IsPointerClick) FocusField(NavigationMethod.Pointer);
                Selection.Deselect(index);
            };
            contents.Children.Add(remove);
        }
        var tag = new Border
        {
            MinHeight = 20, Padding = new Thickness(8, 2), CornerRadius = new CornerRadius(12),
            Child = contents
        };
        tag[!Border.BackgroundProperty] = new DynamicResourceExtension(
            Variant == InputVariant.Secondary ? "AlderSurface" : "AlderNeutral");
        return tag;
    }

    private sealed class TagRemoveButton : AlderButton
    {
        internal bool IsPointerClick { get; private set; }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            IsPointerClick = true;
            try { base.OnPointerPressed(e); }
            finally { IsPointerClick = false; }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            IsPointerClick = true;
            try { base.OnPointerReleased(e); }
            finally { IsPointerClick = false; }
        }
    }

    private void WriteText(string? text)
    {
        settingText = true;
        try
        {
            SetCurrentValue(TextProperty, text);
            if (search is not null) search.SetCurrentValue(TextBox.TextProperty, text);
        }
        finally { settingText = false; }
    }

    private void CreatePopup()
    {
        list = new PickerList(CreateRow) { Focusable = !IsEditable, UsesListBoxRows = UsesListBoxPopup };
        list[!ThemeProperty] = new DynamicResourceExtension(this switch
        {
            AlderModelPicker => "AlderModelPopupListTheme",
            AlderInlineSelect => "AlderInlineSelectListTheme",
            _ => UsesListBoxPopup ? "AlderSelectListTheme" : "AlderPickerListTheme"
        });
        list.AddHandler(PointerReleasedEvent, OnItemReleased, UsesListBoxPopup ? RoutingStrategies.Bubble : RoutingStrategies.Tunnel, handledEventsToo: UsesListBoxPopup);
        status = new AlderTextBlock { Padding = new Thickness(10, 8), TextWrapping = TextWrapping.Wrap, IsVisible = false };
        status[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("AlderMuted");
        panel = new StackPanel();
        panel.AddHandler(KeyDownEvent, (sender, args) =>
        {
            // A footer editor handles its own navigation. Escape still dismisses the parent popup.
            if (args.Key != Key.Escape && this is AlderSelect { DropDownFooter: Visual footer } &&
                args.Source is Visual visual && (visual == footer || footer.IsVisualAncestorOf(visual))) return;
            HandleKey(sender, args);
        }, RoutingStrategies.Tunnel);
        if (HasSearch || this is AlderModelPicker)
        {
            status.TextAlignment = TextAlignment.Center;
            status.LineHeight = 20;
            status.Padding = new Thickness(10);
            search = new AlderSearchField
            {
                FullWidth = true, Variant = InputVariant.Secondary,
                Margin = new Thickness(6, 8, 6, 8)
            };
            if (this is AlderModelPicker model)
            {
                search.Margin = new Thickness(0);
                search.Bind(IsVisibleProperty, model.GetBindingObservable(AlderModelPicker.ShowSearchProperty));
            }
            search.TextChanged += (_, _) =>
            {
                if (!settingText && Text != search.Text) SetCurrentValue(TextProperty, search.Text);
            };
            search.Bind(TextBox.PlaceholderTextProperty, this.GetBindingObservable(SearchPlaceholderTextProperty));
            AutomationProperties.SetName(search, SearchPlaceholderText);
            var spinner = new AlderSpinner
            {
                Size = SpinnerSize.Small, Color = FeedbackColor.Default,
                Width = 16, Height = 16, Margin = new Thickness(0, 0, 14, 0),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            spinner[!ForegroundProperty] = new DynamicResourceExtension("AlderMuted");
            spinner.Bind(IsVisibleProperty, this.GetBindingObservable(IsLoadingProperty));
            panel.Children.Add(new Panel { Children = { search, spinner } });
        }
        if (this is AlderSelect { DropDownHeader: not null } headed)
        {
            var header = new ContentPresenter();
            header.Bind(ContentPresenter.ContentProperty, headed.GetBindingObservable(AlderSelect.DropDownHeaderProperty));
            panel.Children.Add(header);
        }
        panel.Children.Add(list);
        if (UsesListBoxPopup)
        {
            list.AddHandler(PointerPressedEvent, (_, _) => list.ShowKeyboardFocus(false), RoutingStrategies.Tunnel);
            list.AddHandler(ScrollViewer.ScrollChangedEvent, (_, _) => TryLoadMore());
            var spinner = new AlderSpinner { Size = SpinnerSize.Small, Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
            spinner.Bind(IsVisibleProperty, this.GetBindingObservable(IsLoadingProperty));
            status.Padding = new Thickness(0);
            status.VerticalAlignment = VerticalAlignment.Center;
            panel.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Center, Children = { spinner, status },
                IsVisible = false
            });
        }
        else panel.Children.Add(status);
        if (this is AlderSelect select)
        {
            var footer = new ContentPresenter();
            footer.Bind(ContentPresenter.ContentProperty, select.GetBindingObservable(AlderSelect.DropDownFooterProperty));
            panel.Children.Add(footer);
        }
        flyout = new AlderPopover
        {
            Content = panel,
            ContentPadding = UsesListBoxPopup ? default : new Thickness(6, 4),
            SurfaceCornerRadius = new CornerRadius(22),
            Offset = UsesListBoxPopup ? 0 : 6,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            FocusContentOnOpen = false,
            ShowMode = UsesListBoxPopup ? FlyoutShowMode.Transient : FlyoutShowMode.Standard,
            Motion = this is AlderInlineSelect ? AnchoredSurfaceMotion.InlineSelect :
                UsesListBoxPopup ? AnchoredSurfaceMotion.ListBox : HasSearch ? AnchoredSurfaceMotion.Autocomplete : AnchoredSurfaceMotion.Default,
            CustomPopupPlacementCallback = UsesListBoxPopup ? PlaceListBoxPopup : null,
            OverlayDismissEventPassThrough = true,
            MaxContentWidth = double.PositiveInfinity
        };
        flyout.Opened += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsDropDownOpen || flyout?.IsOpen != true) return;
                if (search is { IsVisible: true }) search.Focus();
                else if (UsesListBoxPopup)
                {
                    if ((IsEditable ? (Control?)editor : trigger)?.IsFocused != true) FocusField();
                    if (list.SelectedIndex < 0 && Selection.Count > 0)
                        list.SelectedIndex = rows.FindIndex(entry => IsEnabledOption(entry) && Selection.IsSelected(entry.SourceIndex));
                    if (list.SelectedIndex >= 0) list.ScrollIntoView(list.SelectedIndex);
                    TryLoadMore();
                }
                else FocusField();
            }, DispatcherPriority.Loaded);
        };
        if (UsesListBoxPopup)
        {
            flyout.Bind(AlderPopover.ContentPaddingProperty, this.GetBindingObservable(AlderSelect.DropDownPaddingProperty));
            flyout.Bind(AlderPopover.SurfaceCornerRadiusProperty, this.GetBindingObservable(AlderSelect.DropDownCornerRadiusProperty));
            flyout.Bind(AlderPopover.SurfaceBorderThicknessProperty, this.GetBindingObservable(AlderSelect.DropDownBorderThicknessProperty));
            flyout.Bind(AlderPopover.SurfaceBorderBrushProperty, this.GetBindingObservable(AlderSelect.DropDownBorderBrushProperty));
            flyout.Closing += (_, e) =>
            {
                if (e.Cancel || closing || !IsDropDownOpen) return;
                closing = true;
                SetCurrentValue(IsDropDownOpenProperty, false);
            };
        }
        flyout.Closed += OnPopupClosed;
    }

    private void OpenPopup()
    {
        if (anchor is not { } field || !this.IsAttachedToVisualTree()) return;
        if (closing)
        {
            if (this is AlderCellSelect or AlderInlineSelect or AlderNativeSelect) flyout?.FinishExit();
            return;
        }
        if (flyout?.IsOpen == true) return;
        if (IsReadOnly || !IsEffectivelyEnabled || (ItemsView.Count == 0 && !AllowsEmptyCollection && !IsLoading))
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
            return;
        }

        if (flyout is null) CreatePopup();
        SizePopup();
        panel!.FlowDirection = FlowDirection;
        keepDraftOnClose = false;
        if (UsesListBoxPopup)
        {
            if (ItemContainerTheme is { } itemTheme) list!.ItemContainerTheme = itemTheme;
            else list!.ClearValue(ItemContainerThemeProperty);
            list!.ShowKeyboardFocus(IsSelect && trigger?.Classes.Contains(":focus-visible") == true);
        }
        if (search is not null)
        {
            search.SetCurrentValue(TextBox.TextProperty, Text);
            AutomationProperties.SetName(search, SearchPlaceholderText);
        }
        RefreshRows();
        if (!IsDropDownOpen) return;
        flyout!.Placement = UsesListBoxPopup ? PlacementMode.Custom : FlowDirection == FlowDirection.RightToLeft
            ? PlacementMode.BottomEdgeAlignedRight : PlacementMode.BottomEdgeAlignedLeft;
        flyout.OverlayInputPassThroughElement = field;
        flyout.ShowAt(field);
    }

    private void PlaceListBoxPopup(CustomPopupPlacement placement)
    {
        // Position the visible surface relative to the trigger and leave room for the popup shadow around it.
        bool rtl = FlowDirection == FlowDirection.RightToLeft;
        var alignment = GetValue(AlderSelect.DropDownHorizontalAlignmentProperty);
        bool alignRight = rtl != (alignment == HorizontalAlignment.Right);
        var requested = (this as AlderInlineSelect)?.DropDownPlacement;
        bool horizontal = IsSidePlacement(requested);
        (placement.Anchor, placement.Gravity) = requested switch
        {
            PlacementMode.Top => (PopupAnchor.Top, PopupGravity.Top),
            PlacementMode.TopEdgeAlignedLeft => (PopupAnchor.Top | PopupAnchor.Left, PopupGravity.Top | PopupGravity.Right),
            PlacementMode.TopEdgeAlignedRight => (PopupAnchor.Top | PopupAnchor.Right, PopupGravity.Top | PopupGravity.Left),
            PlacementMode.Bottom => (PopupAnchor.Bottom, PopupGravity.Bottom),
            PlacementMode.BottomEdgeAlignedLeft => (PopupAnchor.Bottom | PopupAnchor.Left, PopupGravity.Bottom | PopupGravity.Right),
            PlacementMode.BottomEdgeAlignedRight => (PopupAnchor.Bottom | PopupAnchor.Right, PopupGravity.Bottom | PopupGravity.Left),
            PlacementMode.Left => (PopupAnchor.Left, PopupGravity.Left),
            PlacementMode.LeftEdgeAlignedTop => (PopupAnchor.Left | PopupAnchor.Top, PopupGravity.Left | PopupGravity.Bottom),
            PlacementMode.LeftEdgeAlignedBottom => (PopupAnchor.Left | PopupAnchor.Bottom, PopupGravity.Left | PopupGravity.Top),
            PlacementMode.Right => (PopupAnchor.Right, PopupGravity.Right),
            PlacementMode.RightEdgeAlignedTop => (PopupAnchor.Right | PopupAnchor.Top, PopupGravity.Right | PopupGravity.Bottom),
            PlacementMode.RightEdgeAlignedBottom => (PopupAnchor.Right | PopupAnchor.Bottom, PopupGravity.Right | PopupGravity.Top),
            _ when alignment == HorizontalAlignment.Center => (PopupAnchor.Bottom, PopupGravity.Bottom),
            _ when alignRight => (PopupAnchor.Bottom | PopupAnchor.Right, PopupGravity.Bottom | PopupGravity.Left),
            _ => (PopupAnchor.Bottom | PopupAnchor.Left, PopupGravity.Bottom | PopupGravity.Right)
        };
        // Reserve a gap on both edges of the trigger. The native positioner can then flip without overlap.
        double gap = GetValue(AlderSelect.DropDownGapProperty);
        placement.AnchorRectangle = placement.AnchorRectangle.Inflate(horizontal ? new Thickness(gap, 0) : new Thickness(0, gap));
        if (this is AlderModelPicker && panel is { Width: var innerWidth } && anchor is { } field &&
            double.IsFinite(innerWidth) && TopLevel.GetTopLevel(this) is { } root &&
            field.TranslatePoint(default, root) is { } origin)
        {
            // Keep a shadow gutter when a centered or trailing composer would push the wider menu offscreen.
            var padding = GetValue(AlderSelect.DropDownPaddingProperty) + GetValue(AlderSelect.DropDownBorderThicknessProperty);
            double width = innerWidth + padding.Left + padding.Right;
            double desired = alignRight ? origin.X + field.Bounds.Width - width : origin.X;
            double gutter = Math.Min(AnchoredSurface.ShadowMargin.Left, Math.Max(0, (root.ClientSize.Width - width) / 2));
            double left = Math.Clamp(desired, gutter, Math.Max(gutter, root.ClientSize.Width - width - gutter));
            placement.AnchorRectangle = placement.AnchorRectangle.Translate(new Vector(left - desired, 0));
        }
        placement.ConstraintAdjustment = (horizontal ? PopupPositionerConstraintAdjustment.FlipX : PopupPositionerConstraintAdjustment.FlipY) |
            PopupPositionerConstraintAdjustment.SlideX | PopupPositionerConstraintAdjustment.SlideY |
            PopupPositionerConstraintAdjustment.ResizeX | PopupPositionerConstraintAdjustment.ResizeY;
    }

    private void SizePopup()
    {
        if (panel is null || list is null || anchor is not { } field) return;
        var root = TopLevel.GetTopLevel(this);
        if (UsesListBoxPopup)
        {
            var inline = this as AlderInlineSelect;
            var inset = GetValue(AlderSelect.DropDownPaddingProperty) + GetValue(AlderSelect.DropDownBorderThicknessProperty);
            var explicitWidth = this is AlderModelPicker ? GetValue(AlderSelect.DropDownMinWidthProperty) : inline?.DropDownWidth;
            double clearance = explicitWidth.HasValue ? AnchoredSurface.ShadowMargin.Left + AnchoredSurface.ShadowMargin.Right : 16;
            double availableWidth = Math.Max(0, (root?.ClientSize.Width ?? double.PositiveInfinity) - clearance - inset.Left - inset.Right);
            double requestedWidth = explicitWidth ?? Math.Max(field.Bounds.Width, GetValue(AlderSelect.DropDownMinWidthProperty));
            double contentWidth = Math.Min(availableWidth, Math.Max(0, requestedWidth - inset.Left - inset.Right));
            // Reset the maximum first when switching to a wider trigger.
            panel.MaxWidth = Math.Max(panel.MinWidth, availableWidth);
            panel.MinWidth = contentWidth;
            panel.Width = explicitWidth.HasValue ? contentWidth : double.NaN;
            panel.MaxWidth = availableWidth;
            var height = root?.ClientSize.Height ?? double.PositiveInfinity;
            if (root is not null && field.TranslatePoint(default, root) is { } top)
            {
                if (!IsSidePlacement(inline?.DropDownPlacement))
                    height = Math.Max(top.Y, height - top.Y - field.Bounds.Height);
                height -= 14;
            }
            var chromeHeight = IsLoading ? 36d : 0;
            if (this is AlderModelPicker)
            {
                chromeHeight = 0;
                foreach (var section in panel.Children)
                {
                    if (section == list || !section.IsVisible) continue;
                    section.Measure(new Size(contentWidth, double.PositiveInfinity));
                    chromeHeight += section.DesiredSize.Height;
                }
            }
            list.MaxHeight = Math.Min(MaxDropDownHeight, Math.Max(36, height - inset.Top - inset.Bottom - chromeHeight));
        }
        else
        {
            panel.Width = Math.Max(0, field.Bounds.Width - 12);
            var height = root?.ClientSize.Height ?? double.PositiveInfinity;
            list.MaxHeight = Math.Min(MaxDropDownHeight, Math.Max(36, height - (HasSearch ? 68 : 16)));
        }
    }

    private static bool IsSidePlacement(PlacementMode? placement) => placement is
        PlacementMode.Left or PlacementMode.LeftEdgeAlignedTop or PlacementMode.LeftEdgeAlignedBottom or
        PlacementMode.Right or PlacementMode.RightEdgeAlignedTop or PlacementMode.RightEdgeAlignedBottom;

    private void TryLoadMore()
    {
        if (!UsesListBoxPopup || !IsDropDownOpen || IsLoading ||
            GetValue(AlderSelect.LoadMoreCommandProperty) is not { } command) return;
        if (rows.Count == 0 || list?.ScrollHost is { Viewport.Height: > 0 } scroll &&
            scroll.Extent.Height - scroll.Viewport.Height - scroll.Offset.Y <= 72)
        {
            if (command.CanExecute(null)) command.Execute(null);
        }
    }

    private void ClosePopup()
    {
        if (flyout?.IsOpen != true || closing) return;
        closing = true;
        flyout.Hide();
    }

    private void Dismiss(bool restoreFocus, NavigationMethod focusMethod = NavigationMethod.Tab)
    {
        if (restoreFocus) FocusField(focusMethod);
        SetCurrentValue(IsDropDownOpenProperty, false);
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        bool reopen = closing && IsDropDownOpen;
        bool preserveDraft = keepDraftOnClose;
        keepDraftOnClose = false;
        closing = false;
        if (reopen && !templateChanging)
        {
            Dispatcher.UIThread.Post(OpenPopup, DispatcherPriority.Input);
            return;
        }
        if (IsDropDownOpen) SetCurrentValue(IsDropDownOpenProperty, false);
        if (list is not null) list.SelectedIndex = -1;
        if (search is not null) WriteText(null);
        if (IsEditable && !IsMultiple && !preserveDraft && !IsReadOnly) FinishEditing();
    }

    private void FinishEditing()
    {
        if (AllowsCustomValue && !string.Equals(Text, GetText(SelectedItem), StringComparison.Ordinal))
        {
            settingText = true;
            try { Selection.Clear(); }
            finally { settingText = false; }
            committedCustomText = Text;
        }
        else if (!AllowsCustomValue)
            WriteText(SelectedIndex >= 0 ? GetText(SelectedItem) : null);
    }

    private void FocusField(NavigationMethod focusMethod = NavigationMethod.Tab)
    {
        changingFocus = true;
        try { (IsEditable ? editor as Control : trigger)?.Focus(focusMethod); }
        finally { changingFocus = false; }
    }

    private void RefreshRows()
    {
        if (list is null || status is null) return;
        int activeItem = list.SelectedItem is PickerEntry active ? active.SourceIndex : -1;
        string query = (IsSelect && !HasSearch) || (IsEditable && showUnfilteredItems) ? string.Empty : Text?.Trim() ?? string.Empty;
        if (IsEditable && SelectedIndex >= 0 && !IsMultiple && string.Equals(query, GetText(SelectedItem), StringComparison.Ordinal))
            query = string.Empty;
        bool queryChanged = rowsQuery != query;
        bool shouldFilter = query.Length > 0 || this is AlderComboBox { LoadMoreCommand: not null };
        var result = new List<PickerEntry>(shouldFilter ? 0 : ItemsView.Count);
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        var filter = Filter;
        string? group = null;
        bool hasSection = false;
        for (int index = 0; index < ItemsView.Count; index++)
        {
            var item = ItemsView[index];
            if (shouldFilter && !(filter?.Invoke(query, item) ?? Matches(query, item, compare))) continue;
            var nextGroup = (item as AlderPickerOption)?.Group;
            if (!string.IsNullOrEmpty(nextGroup) && nextGroup != group)
            {
                if (hasSection && this is not AlderModelPicker) result.Add(new PickerEntry(-2));
                result.Add(new PickerEntry(-1, nextGroup));
                hasSection = true;
            }
            group = nextGroup;
            result.Add(new PickerEntry(index, item));
        }
        if (IsEditable && IsDropDownOpen && result.Count == 0 && !AllowsEmptyCollection && !IsLoading)
        {
            keepDraftOnClose = true;
            SetCurrentValue(IsDropDownOpenProperty, false);
            return;
        }
        rows = result;
        rowsQuery = query;
        if (UsesListBoxPopup) list.Classes.Set("sections", hasSection);
        SizePopup();
        var offset = !queryChanged && UsesListBoxPopup && query.Length == 0 ? list.ScrollHost?.Offset : null;
        list.ItemsSource = result;
        list.SelectedIndex = activeItem < 0 ? -1 : result.FindIndex(entry => entry.SourceIndex == activeItem && IsEnabledOption(entry));
        list.IsVisible = result.Count > 0;
        status.IsVisible = IsLoading || result.Count == 0;
        status.Text = IsLoading ? LoadingText : EmptyText;
        if (UsesListBoxPopup && status.Parent is Panel footer)
            footer.IsVisible = status.IsVisible;
        if (list.ScrollHost is { } scroll)
        {
            if (queryChanged)
            {
                scroll.Offset = default;
                if (list.SelectedIndex >= 0)
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (IsDropDownOpen && ReferenceEquals(rows, result)) list.ScrollIntoView(list.SelectedIndex);
                    }, DispatcherPriority.Loaded);
            }
            else if (offset is { Y: > 0 } savedOffset)
                Dispatcher.UIThread.Post(() =>
                {
                    if (IsDropDownOpen && ReferenceEquals(rows, result)) scroll.Offset = savedOffset;
                }, DispatcherPriority.Loaded);
        }
        if (search is not null) search.ShowClearButton = !IsLoading;
    }

    private bool Matches(string query, object? item, CompareInfo compare)
    {
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        return compare.IndexOf(GetText(item), query, options) >= 0 ||
            this is AlderModelPicker && item is AlderPickerOption { Group: { } provider } && compare.IndexOf(provider, query, options) >= 0;
    }

    private bool IsEnabledOption(PickerEntry entry) => (uint)entry.SourceIndex < (uint)ItemsView.Count &&
        entry.Item is not AlderPickerOption { IsEnabled: false };

    private PickerRow CreateRow(PickerEntry entry)
    {
        int index = entry.SourceIndex;
        if (index < 0) return new PickerRow(index, entry.Item as string ?? string.Empty, null, null, null, null, false, false);

        var item = entry.Item;
        var option = item as AlderPickerOption;
        return new PickerRow(index, GetText(item), option?.Description, option?.Image, item, ItemTemplate,
            option?.IsEnabled ?? true, Selection.IsSelected(index))
        {
            UsesListBoxRows = UsesListBoxPopup, AnimateCheck = IsSelect && IsMultiple, IsInSection = option?.Group is { Length: > 0 }
        };
    }

    private void OnItemReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!IsDropDownOpen || e.InitialPressMouseButton != MouseButton.Left || e.Source is not Visual source ||
            source.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.Content is not PickerRow { IsEnabled: true } row)
            return;
        Commit(row.SourceIndex, NavigationMethod.Pointer);
        e.Handled = true;
    }

    private void Commit(int index, NavigationMethod focusMethod = NavigationMethod.Tab)
    {
        if (IsReadOnly || (uint)index >= (uint)ItemsView.Count) return;
        if (IsMultiple)
        {
            if (Selection.IsSelected(index)) Selection.Deselect(index);
            else Selection.Select(index);
            if (IsEditable)
            {
                bool refresh = !string.IsNullOrEmpty(Text);
                showUnfilteredItems = true;
                WriteText(null);
                if (refresh) RefreshRows();
                if (editor?.IsFocused != true) FocusField();
            }
            return;
        }

        SetCurrentValue(SelectedIndexProperty, index);
        if (IsEditable)
        {
            WriteText(GetText(SelectedItem));
            if (editor is not null) editor.CaretIndex = Text?.Length ?? 0;
        }
        Dismiss(restoreFocus: true, focusMethod);
    }

    private void HandleKey(object? sender, KeyEventArgs e)
    {
        if (IsReadOnly || e.Handled) return;
        switch (e.Key)
        {
            case Key.Back when IsSelect && ShowClearButton && !IsDropDownOpen && e.KeyModifiers == KeyModifiers.None:
            case Key.Delete when IsSelect && ShowClearButton && !IsDropDownOpen && e.KeyModifiers == KeyModifiers.None:
                ClearSelection();
                e.Handled = true;
                break;
            case Key.F4:
                TogglePopup(sender, e);
                e.Handled = true;
                break;
            case Key.Down:
            case Key.Up:
                if (!IsDropDownOpen)
                {
                    if (IsEditable) showUnfilteredItems = true;
                    SetCurrentValue(IsDropDownOpenProperty, true);
                }
                if (IsDropDownOpen) MoveActive(e.Key == Key.Down ? 1 : -1);
                e.Handled = true;
                break;
            case Key.Home when IsDropDownOpen && !IsEditable && search?.IsFocused != true:
            case Key.End when IsDropDownOpen && !IsEditable && search?.IsFocused != true:
                MoveActive(e.Key == Key.Home ? 1 : -1, edge: true);
                e.Handled = true;
                break;
            case Key.Enter when IsDropDownOpen:
            case Key.Space when IsDropDownOpen && this is AlderSelect && search?.IsFocused != true:
                if (list?.SelectedItem is PickerEntry entry && IsEnabledOption(entry)) Commit(entry.SourceIndex);
                else if (IsEditable && AllowsCustomValue) Dismiss(restoreFocus: true);
                e.Handled = true;
                break;
            case Key.Escape when IsDropDownOpen:
                if (IsEditable) RestoreEditorText();
                Dismiss(restoreFocus: true);
                e.Handled = true;
                break;
            case Key.Escape when IsEditable && editor?.IsFocused == true:
                RestoreEditorText();
                e.Handled = true;
                break;
            case Key.Enter when IsEditable && AllowsCustomValue:
                FinishEditing();
                break;
            case Key.Tab when IsDropDownOpen && e.KeyModifiers == KeyModifiers.None &&
                this is AlderModelPicker { DropDownFooter: Control footer } &&
                footer.GetVisualDescendants().OfType<Slider>().FirstOrDefault(slider => slider.IsEffectivelyVisible && slider.IsEnabled) is { } effort:
                e.Handled = effort.Focus(NavigationMethod.Tab);
                break;
            case Key.Tab when IsDropDownOpen:
                Dismiss(restoreFocus: search?.IsFocused == true);
                break;
        }
    }

    private void RestoreEditorText() => WriteText(IsMultiple ? null :
        SelectedIndex >= 0 ? GetText(SelectedItem) : AllowsCustomValue ? committedCustomText : null);

    private void HandleTypeAhead(object? sender, TextInputEventArgs e)
    {
        if (IsReadOnly || !IsEffectivelyEnabled || string.IsNullOrWhiteSpace(e.Text)) return;
        long now = Environment.TickCount64;
        typedPrefix = now - lastTypedAt > 800 ? e.Text : typedPrefix + e.Text;
        lastTypedAt = now;
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        if (IsDropDownOpen && list is not null)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var entry = rows[i];
                if (!IsEnabledOption(entry) || !compare.IsPrefix(GetText(entry.Item), typedPrefix, options)) continue;
                if (IsSelect) list.ShowKeyboardFocus(true);
                list.SelectedIndex = i;
                list.ScrollIntoView(i);
                e.Handled = true;
                return;
            }
        }
        else if (!IsMultiple)
        {
            for (int i = 0; i < ItemsView.Count; i++)
            {
                var item = ItemsView[i];
                if (item is AlderPickerOption { IsEnabled: false } || !compare.IsPrefix(GetText(item), typedPrefix, options)) continue;
                SetCurrentValue(SelectedIndexProperty, i);
                e.Handled = true;
                return;
            }
        }
    }

    private void MoveActive(int direction, bool edge = false)
    {
        if (list is null || rows.Count == 0) return;
        if (UsesListBoxPopup) list.ShowKeyboardFocus(true);
        int current = list.SelectedIndex;
        int index = edge || current < 0 ? (direction > 0 ? 0 : rows.Count - 1) : current + direction;
        for (; index >= 0 && index < rows.Count; index += direction)
        {
            if (!IsEnabledOption(rows[index])) continue;
            list.SelectedIndex = index;
            list.ScrollIntoView(index);
            break;
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new PickerPeer(this);

    private sealed class PickerPeer(AlderPicker owner) : ControlAutomationPeer(owner), IExpandCollapseProvider
    {
        public ExpandCollapseState ExpandCollapseState => owner.IsDropDownOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
        public bool ShowsMenu => true;
        public void Expand() => owner.SetCurrentValue(IsDropDownOpenProperty, true);
        public void Collapse() => owner.SetCurrentValue(IsDropDownOpenProperty, false);
        protected override string? GetNameCore() => AutomationProperties.GetName(owner) ?? owner.Label;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox;

    }
}

/// <summary>Presents an options list for choosing values without a text editor.</summary>
public partial class AlderSelect : AlderPicker
{
    /// <summary>Identifies the <see cref="DropDownMinWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> DropDownMinWidthProperty =
        AvaloniaProperty.Register<AlderSelect, double>(nameof(DropDownMinWidth), validate: value => double.IsFinite(value) && value >= 0);
    /// <summary>Identifies the <see cref="DropDownGap"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> DropDownGapProperty =
        AvaloniaProperty.Register<AlderSelect, double>(nameof(DropDownGap), ListBoxPopupGap, validate: value => double.IsFinite(value) && value >= 0);
    /// <summary>Identifies the <see cref="DropDownHorizontalAlignment"/> Avalonia property.</summary>
    public static readonly StyledProperty<HorizontalAlignment> DropDownHorizontalAlignmentProperty =
        AvaloniaProperty.Register<AlderSelect, HorizontalAlignment>(nameof(DropDownHorizontalAlignment), HorizontalAlignment.Left);
    /// <summary>Identifies the <see cref="DropDownCornerRadius"/> Avalonia property.</summary>
    public static readonly StyledProperty<CornerRadius> DropDownCornerRadiusProperty =
        AvaloniaProperty.Register<AlderSelect, CornerRadius>(nameof(DropDownCornerRadius), new CornerRadius(22));
    /// <summary>Identifies the <see cref="DropDownPadding"/> Avalonia property.</summary>
    public static readonly StyledProperty<Thickness> DropDownPaddingProperty =
        AvaloniaProperty.Register<AlderSelect, Thickness>(nameof(DropDownPadding));
    /// <summary>Identifies the <see cref="DropDownBorderBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> DropDownBorderBrushProperty =
        AvaloniaProperty.Register<AlderSelect, IBrush?>(nameof(DropDownBorderBrush));
    /// <summary>Identifies the <see cref="DropDownBorderThickness"/> Avalonia property.</summary>
    public static readonly StyledProperty<Thickness> DropDownBorderThicknessProperty =
        AvaloniaProperty.Register<AlderSelect, Thickness>(nameof(DropDownBorderThickness));
    /// <summary>Identifies the <see cref="LoadMoreCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> LoadMoreCommandProperty =
        AvaloniaProperty.Register<AlderSelect, ICommand?>(nameof(LoadMoreCommand));

    /// <summary>Optional heading above the option list.</summary>
    [GeneratedStyledProperty] public partial object? DropDownHeader { get; set; }

    /// <summary>Optional content below the option list, for related controls such as reasoning effort.</summary>
    [GeneratedStyledProperty] public partial object? DropDownFooter { get; set; }

    static AlderSelect() => MaxDropDownHeightProperty.OverrideDefaultValue<AlderSelect>(double.PositiveInfinity);

    /// <summary>The minimum width of the options popup.</summary>
    public double DropDownMinWidth { get => GetValue(DropDownMinWidthProperty); set => SetValue(DropDownMinWidthProperty, value); }
    /// <summary>The distance between the field and the options popup.</summary>
    public double DropDownGap { get => GetValue(DropDownGapProperty); set => SetValue(DropDownGapProperty, value); }
    /// <summary>The horizontal alignment of the options popup relative to the field.</summary>
    public HorizontalAlignment DropDownHorizontalAlignment { get => GetValue(DropDownHorizontalAlignmentProperty); set => SetValue(DropDownHorizontalAlignmentProperty, value); }
    /// <summary>The corner radius of the options popup.</summary>
    public CornerRadius DropDownCornerRadius { get => GetValue(DropDownCornerRadiusProperty); set => SetValue(DropDownCornerRadiusProperty, value); }
    /// <summary>The padding inside the options popup.</summary>
    public Thickness DropDownPadding { get => GetValue(DropDownPaddingProperty); set => SetValue(DropDownPaddingProperty, value); }
    /// <summary>The brush used for the options popup border.</summary>
    public IBrush? DropDownBorderBrush { get => GetValue(DropDownBorderBrushProperty); set => SetValue(DropDownBorderBrushProperty, value); }
    /// <summary>The thickness of the options popup border.</summary>
    public Thickness DropDownBorderThickness { get => GetValue(DropDownBorderThicknessProperty); set => SetValue(DropDownBorderThicknessProperty, value); }

    /// <summary>Requests another page when the open list reaches the end of its loaded items.</summary>
    public ICommand? LoadMoreCommand { get => GetValue(LoadMoreCommandProperty); set => SetValue(LoadMoreCommandProperty, value); }
}
/// <summary>Presents filtered suggestions while the user edits a text value.</summary>
public sealed class AlderAutocomplete : AlderPicker
{
    /// <summary>Identifies the <see cref="ShowSelectionTags"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowSelectionTagsProperty =
        AvaloniaProperty.Register<AlderAutocomplete, bool>(nameof(ShowSelectionTags));

    static AlderAutocomplete() => MaxDropDownHeightProperty.OverrideDefaultValue<AlderAutocomplete>(312);

    /// <summary>Displays individually removable tags when multiple items are selected.</summary>
    public bool ShowSelectionTags { get => GetValue(ShowSelectionTagsProperty); set => SetValue(ShowSelectionTagsProperty, value); }
}
/// <summary>Combines an editable value with a separately configurable drop-down list.</summary>
public sealed class AlderComboBox : AlderPicker
{
    /// <summary>Identifies the <see cref="DropDownMinWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> DropDownMinWidthProperty =
        AvaloniaProperty.Register<AlderSelect, double>(nameof(DropDownMinWidth), validate: value => double.IsFinite(value) && value >= 0);
    /// <summary>Identifies the <see cref="DropDownGap"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> DropDownGapProperty =
        AvaloniaProperty.Register<AlderSelect, double>(nameof(DropDownGap), ListBoxPopupGap, validate: value => double.IsFinite(value) && value >= 0);
    /// <summary>Identifies the <see cref="DropDownHorizontalAlignment"/> Avalonia property.</summary>
    public static readonly StyledProperty<HorizontalAlignment> DropDownHorizontalAlignmentProperty =
        AvaloniaProperty.Register<AlderSelect, HorizontalAlignment>(nameof(DropDownHorizontalAlignment), HorizontalAlignment.Left);
    /// <summary>Identifies the <see cref="DropDownCornerRadius"/> Avalonia property.</summary>
    public static readonly StyledProperty<CornerRadius> DropDownCornerRadiusProperty =
        AlderSelect.DropDownCornerRadiusProperty.AddOwner<AlderComboBox>();
    /// <summary>Identifies the <see cref="DropDownPadding"/> Avalonia property.</summary>
    public static readonly StyledProperty<Thickness> DropDownPaddingProperty =
        AlderSelect.DropDownPaddingProperty.AddOwner<AlderComboBox>();
    /// <summary>Identifies the <see cref="DropDownBorderBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> DropDownBorderBrushProperty =
        AlderSelect.DropDownBorderBrushProperty.AddOwner<AlderComboBox>();
    /// <summary>Identifies the <see cref="DropDownBorderThickness"/> Avalonia property.</summary>
    public static readonly StyledProperty<Thickness> DropDownBorderThicknessProperty =
        AlderSelect.DropDownBorderThicknessProperty.AddOwner<AlderComboBox>();
    /// <summary>Identifies the <see cref="LoadMoreCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> LoadMoreCommandProperty =
        AlderSelect.LoadMoreCommandProperty.AddOwner<AlderComboBox>();

    static AlderComboBox() => MaxDropDownHeightProperty.OverrideDefaultValue<AlderComboBox>(double.PositiveInfinity);

    /// <summary>The minimum width of the options popup.</summary>
    public double DropDownMinWidth { get => GetValue(DropDownMinWidthProperty); set => SetValue(DropDownMinWidthProperty, value); }
    /// <summary>The distance between the field and the options popup.</summary>
    public double DropDownGap { get => GetValue(DropDownGapProperty); set => SetValue(DropDownGapProperty, value); }
    /// <summary>The horizontal alignment of the options popup relative to the field.</summary>
    public HorizontalAlignment DropDownHorizontalAlignment { get => GetValue(DropDownHorizontalAlignmentProperty); set => SetValue(DropDownHorizontalAlignmentProperty, value); }
    /// <summary>The corner radius of the options popup.</summary>
    public CornerRadius DropDownCornerRadius { get => GetValue(DropDownCornerRadiusProperty); set => SetValue(DropDownCornerRadiusProperty, value); }
    /// <summary>The padding inside the options popup.</summary>
    public Thickness DropDownPadding { get => GetValue(DropDownPaddingProperty); set => SetValue(DropDownPaddingProperty, value); }
    /// <summary>The brush used for the options popup border.</summary>
    public IBrush? DropDownBorderBrush { get => GetValue(DropDownBorderBrushProperty); set => SetValue(DropDownBorderBrushProperty, value); }
    /// <summary>The thickness of the options popup border.</summary>
    public Thickness DropDownBorderThickness { get => GetValue(DropDownBorderThicknessProperty); set => SetValue(DropDownBorderThicknessProperty, value); }

    /// <summary>Requests another page when the open list reaches the end of the current query.</summary>
    public ICommand? LoadMoreCommand { get => GetValue(LoadMoreCommandProperty); set => SetValue(LoadMoreCommandProperty, value); }
}

// Entries reference either a source item or a section caption. Only realized rows get a binding object.
internal readonly record struct PickerEntry(int SourceIndex, object? Item = null);

internal sealed class PickerRow(int index, string text, string? description, IImage? image, object? item,
    IDataTemplate? template, bool enabled, bool committed) : AvaloniaObject
{
    public static readonly DirectProperty<PickerRow, bool> IsCommittedProperty =
        AvaloniaProperty.RegisterDirect<PickerRow, bool>(nameof(IsCommitted), row => row.IsCommitted);

    public int SourceIndex { get; } = index;
    public string Text { get; } = text;
    public string? Description { get; } = description;
    public IImage? Image { get; } = image;
    public object? Item { get; } = item;
    public IDataTemplate? Template { get; } = template;
    public bool IsEnabled { get; } = enabled;
    public bool IsCommitted { get => field; set => SetAndRaise(IsCommittedProperty, ref field, value); } = committed;
    public bool IsHeader => SourceIndex == -1;
    public bool IsSeparator => SourceIndex == -2;
    public bool IsOption => SourceIndex >= 0;
    public bool HasTemplate => Template is not null;
    public bool UsesListBoxRows { get; init; }
    public bool AnimateCheck { get; init; }
    public bool IsInSection { get; init; }
}

internal sealed class PickerList(Func<PickerEntry, PickerRow> createRow) : ListBox
{
    private bool focusVisible;
    public bool UsesListBoxRows { get; init; }
    public ScrollViewer? ScrollHost { get; private set; }

    protected override Type StyleKeyOverride => typeof(ListBox);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ScrollHost = e.NameScope.Find<ScrollViewer>("PART_PickerScroll");
    }

    public void ShowKeyboardFocus(bool visible)
    {
        if (focusVisible == visible) return;
        focusVisible = visible;
        foreach (var container in GetRealizedContainers())
            container.Classes.Set("pointer-mode", !visible);
    }

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        if (item is not PickerEntry entry)
        {
            base.PrepareContainerForItemOverride(container, item, index);
            return;
        }
        var row = createRow(entry);
        base.PrepareContainerForItemOverride(container, row, index);
        container.DataContext = row;
        container.IsEnabled = row.IsEnabled;
        container.Classes.Set("section", row.IsHeader);
        container.Classes.Set("separator", row.IsSeparator);
        if (UsesListBoxRows)
        {
            container.Focusable = false;
            container.Classes.Set("pointer-mode", !focusVisible);
            container.Classes.Set("group-option", row.IsInSection);
        }
        AutomationProperties.SetName(container, row.Text);
        AutomationProperties.SetAccessibilityView(container, row.IsOption ? AccessibilityView.Content : AccessibilityView.Raw);
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        container.ClearValue(DataContextProperty);
    }
}
