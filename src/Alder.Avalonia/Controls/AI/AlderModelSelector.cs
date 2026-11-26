using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>A model picker with an optional independent reasoning effort selector.</summary>
public sealed partial class AlderModelSelector : TemplatedControl
{
    /// <summary>Shows a search field for finding a model by name or provider.</summary>
    [GeneratedStyledProperty] public partial bool ShowSearch { get; set; }

    /// <summary>Optional note, such as an explanation of pricing, below the model list.</summary>
    [GeneratedStyledProperty] public partial object? EffortNote { get; set; }

    /// <summary>Uses the accent rail for the two highest effort tiers.</summary>
    public static FuncValueConverter<object?, bool> IsHighEffort { get; } = new(effort => effort?.ToString() is "Max" or "Ultra");

    /// <summary>Identifies the highest tier, which adds the Ultra glow and sparks.</summary>
    public static FuncValueConverter<object?, bool> IsUltraEffort { get; } = new(effort => effort?.ToString() == "Ultra");

    /// <summary>Shows an effort slider only when at least two levels can be selected.</summary>
    public static FuncValueConverter<int, bool> HasEffortChoices { get; } = new(count => count > 1);

    /// <summary>Displays the compact model caption while retaining the full option name in the popup.</summary>
    public static FuncValueConverter<object?, string?> Caption { get; } = new(item =>
        item is AlderPickerOption { ShortText: { } caption } ? caption : item?.ToString());

    /// <summary>Shows the selected model image or the supplied leading content inside the trigger.</summary>
    [GeneratedStyledProperty(DefaultValue = true)] public partial bool ShowIcon { get; set; }
    /// <summary>Shows the animated sparks around the track on Ultra.</summary>
    [GeneratedStyledProperty(DefaultValue = true)] public partial bool ShowOuterSparks { get; set; }

    /// <summary>Optional trigger content in place of the selected model image.</summary>
    [GeneratedStyledProperty] public partial object? LeadingContent { get; set; }
    /// <summary>Uses the same model image in the trigger and the option list.</summary>
    public static FuncValueConverter<object?, IImage?> ModelImage { get; } = new(item => (item as AlderPickerOption)?.Image);
    /// <summary>Largest slider index for the current list of effort levels.</summary>
    public static FuncValueConverter<int, double> EffortMaximum { get; } = new(count => Math.Max(0, count - 1));

    /// <summary>Presentation of a model row inside the popup.</summary>
    [GeneratedStyledProperty] public partial IDataTemplate? ItemTemplate { get; set; }

    /// <summary>Available models. AlderPickerOption items may supply a shared image, groups, descriptions and availability.</summary>
    [GeneratedStyledProperty] public partial IEnumerable? ItemsSource { get; set; }
    /// <summary>The selected model object.</summary>
    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)] public partial object? SelectedModel { get; set; }
    /// <summary>Available effort levels for the selected model.</summary>
    /// <remarks>List the levels in ascending order. The slider appears when two or more levels are available.</remarks>
    [GeneratedStyledProperty] public partial IEnumerable? Efforts { get; set; }
    /// <summary>The selected reasoning effort.</summary>
    /// <remarks>The two-way binding updates as the slider crosses each discrete step.</remarks>
    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)] public partial object? SelectedEffort { get; set; }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        // The option list owns a popup visual root. Bind its footer to this selector across that boundary.
        if (e.NameScope.Find<AlderSelect>("PART_Models")?.DropDownFooter is Control footer)
            footer.DataContext = this;
    }
}


// Keeps model-specific popup defaults out of the general-purpose Select.
internal sealed partial class AlderModelPicker : AlderSelect
{
    [GeneratedStyledProperty] public partial bool ShowSearch { get; set; }

    public static readonly StyledProperty<bool> ShowIconProperty = AlderModelSelector.ShowIconProperty.AddOwner<AlderModelPicker>();
    public static readonly StyledProperty<object?> LeadingContentProperty = AlderModelSelector.LeadingContentProperty.AddOwner<AlderModelPicker>();

    public bool ShowIcon { get => GetValue(ShowIconProperty); set => SetValue(ShowIconProperty, value); }
    public object? LeadingContent { get => GetValue(LeadingContentProperty); set => SetValue(LeadingContentProperty, value); }

    protected override Type StyleKeyOverride => typeof(AlderSelect);

    public AlderModelPicker()
    {
        this[!SearchPlaceholderTextProperty] = new DynamicResourceExtension("AlderText.AiModelSearch");
        this[!EmptyTextProperty] = new DynamicResourceExtension("AlderText.AiNoModels");
    }
}
