using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.ViewModels;

namespace Alder.Avalonia.Gallery.Localization;

public sealed record GalleryLanguage(string Tag, string Name, DayOfWeek FirstDayOfWeek);

public sealed record GalleryMessage(string Key, params object?[] Arguments)
{
    public static implicit operator GalleryMessage(string key) => new(key);
}

internal sealed class GalleryLocalization
{
    public static IReadOnlyList<GalleryLanguage> Languages { get; } =
    [
        new("en-US", "English", DayOfWeek.Sunday),
        new("zh-CN", "简体中文", DayOfWeek.Monday)
    ];

    internal static GalleryLanguage DefaultLanguage => Languages[0];

    private static readonly string[] ControlKeys =
    [
        "AiDismiss", "AiApprove", "AiReject", "AiApproved", "AiRejected", "AiAsk", "AiSend", "AiStop", "AiAttach", "AiRemove", "AiCopy", "AiCopyCode", "AiJump", "AiConversation",
        "AiLike", "AiDislike", "AiRetry", "AiThinking", "AiTool", "AiModel", "AiEffort", "AiModelSearch", "AiNoModels", "AiMaxEffortHint", "AiUltraEffortHint", "AiModels", "AiFaster", "AiSmarter", "AiLoad", "AiStepComplete", "AiStepActive", "AiStepPending", "AiStepError", "AiStepInterrupted", "AiArguments", "AiResult",
        "AiReasoningCompleted", "AiReasoningCompletedFor", "AiReasoningStopped", "AiReasoningStoppedFor", "AiReasoningFailed",
        "RichEditor", "RichPlaceholder", "RichCounts", "RichCharacters", "RichUndo", "RichRedo",
        "RichBold", "RichItalic", "RichUnderline", "RichStrike", "RichCode", "RichHeading1",
        "RichHeading2", "RichHeading3", "RichQuote", "RichCodeBlock", "RichBulletList", "RichOrderedList",
        "RichLink", "RichLinkUrl", "RichClearFormatting", "RichClearContent", "RichNoCommands", "RichRemove", "RichApply",
        "RichHeading1Hint", "RichHeading2Hint", "RichHeading3Hint", "RichBulletListHint", "RichOrderedListHint", "RichQuoteHint", "RichCodeBlockHint",
        "Pagination", "PreviousPage", "NextPage", "PageNumberFormat", "CurrentPageNumberFormat", "PreviousTabs", "NextTabs", "CloseTab",
        "BreadcrumbNavigation", "CurrentPage", "OpenCalendar", "OpenRangeCalendar", "StartDate", "EndDate", "ChooseYear",
        "PreviousPeriod", "NextPeriod", "LowerValue", "UpperValue", "DecreaseValue",
        "IncreaseValue", "Increase", "Decrease", "Close", "ClearSearch",
        "Dialog.OK", "Dialog.Cancel", "Dialog.Yes", "Dialog.No", "ResizeSheet",
        "ActionBarActions", "FileTreeEmpty", "FileTreeExpand", "FileTreeSelect", "FileTreeMove", "FloatingTocNavigation",
        "AgendaDay", "AgendaWeek", "AgendaMonth", "AgendaToday", "AgendaView", "AgendaTimeGrid",
        "AgendaAllDay", "AgendaCollapseAllDay", "AgendaExpandAllDay", "AgendaEventCount", "AgendaMore",
        "CarouselPrevious", "CarouselNext", "CarouselGoToSlide", "CarouselPlay", "CarouselPause",
        "CommandPalette", "CommandSearch", "CommandEmpty", "CommandBack", "CommandDismiss",
        "NavbarNavigation", "NavbarOpenMenu", "NavbarCloseMenu", "StepperProgress", "StepperActive", "StepperComplete", "StepperInactive",
        "Unavailable", "Selected", "Loading", "Year", "Month", "Day", "Hour", "Minute", "SecondUnit", "Period",
        "DropBrowse", "DropTitle", "DropRemove", "DropRetry", "DropUploading", "DropComplete", "DropFailed",
        "DropTypeError", "DropSizeError", "DropCountError", "DropUnavailable",
        "WindowMinimize", "WindowMaximize", "WindowRestore",
        "YearPlaceholder", "MonthPlaceholder", "DayPlaceholder", "HourPlaceholder", "MinutePlaceholder", "SecondPlaceholder",
        "PickerPlaceholder", "PickerSearch", "PickerEmpty", "PickerLoading", "PickerOpen", "PickerClear", "PickerRemove", "SelectSeparator", "SelectPair", "SelectListEnd",
        "EmojiOpen", "EmojiSearch", "EmojiGrid", "EmojiEmpty", "EmojiSkinTone", "EmojiRecent",
        "EmojiToneDefault", "EmojiToneLight", "EmojiToneMediumLight", "EmojiToneMedium", "EmojiToneMediumDark", "EmojiToneDark",
        "EmojiSmileys", "EmojiPeople", "EmojiAnimals", "EmojiFood", "EmojiActivities", "EmojiTravel", "EmojiObjects", "EmojiSymbols", "EmojiFlags",
        "TableEmpty", "TableSelectAll", "TableSelectRow", "TableResizeColumn", "TableExpandRow", "TableCollapseRow", "DataGridDragRow",
        "KanbanMove", "MapZoomIn", "MapZoomOut", "MapResetBearing", "MapLocate", "MapFullScreen", "MapExitFullScreen", "MapClosePopup",
        "ColorArea", "ColorHue", "ColorSaturation", "ColorLightness", "ColorBrightness", "ColorAlpha",
        "ColorRed", "ColorGreen", "ColorBlue", "ColorValue", "ColorInvalid", "ColorApply", "ColorChoose",
        "AppLayoutToggleSidebar", "AppLayoutToggleAside", "AppLayoutCloseSidebar", "AppLayoutCloseAside", "SidebarToggle",
        "SidebarSearchNavigation", "SidebarSearchEmpty", "RatingLabel", "TrendUp", "TrendDown", "TrendNeutral", "ResizableHandle",
        "AppLayoutResizeSidebar", "AppLayoutResizeAside",
        "UserMenuAccount", "UserMenuTheme", "UserMenuLight", "UserMenuDark", "UserMenuSystem", "UserMenuLogOut"
    ];

    internal static GalleryLocalization Current => (Application.Current as App)?.Localization ?? Fallback.Value;
    private static readonly Lazy<GalleryLocalization> Fallback = new(() => new());

    private readonly List<WeakReference<GalleryViewModel>> models = [];
    private readonly Dictionary<string, Dictionary<string, string>> catalogs = new(StringComparer.Ordinal);
    private ResourceDictionary? activeResources;

    public GalleryLanguage Language { get; private set; } = DefaultLanguage;
    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo(DefaultLanguage.Tag);

    public void Apply(GalleryLanguage language, IResourceDictionary resources)
    {
        if (activeResources is not null && Language == language) return;
        var english = Catalog(DefaultLanguage.Tag);
        var translated = Catalog(language.Tag);
        Language = language;
        // Match calendar week starts to CLDR across platforms with different ICU versions.
        var culture = new CultureInfo(language.Tag, useUserOverride: false);
        culture.DateTimeFormat.FirstDayOfWeek = language.FirstDayOfWeek;
        Culture = CultureInfo.ReadOnly(culture);
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;

        var dictionary = new ResourceDictionary
        {
            ["GalleryCulture"] = Culture,
            ["GalleryNumberFormat"] = Culture.NumberFormat,
            ["GalleryFlowDirection"] = Culture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            ["AlderText.EditCut"] = Text("Cut"),
            ["AlderText.EditCopy"] = Text("Copy"),
            ["AlderText.EditPaste"] = Text("ContextMenuPaste")
        };
        foreach (var (key, fallback) in english)
            dictionary["GalleryText." + key] = translated.GetValueOrDefault(key, fallback);
        foreach (var key in ControlKeys)
            dictionary["AlderText." + key] = Text(key);
        foreach (var key in Enum.GetValues<KbdKey>())
        {
            if (key != KbdKey.None)
                dictionary[$"AlderText.Kbd{key}"] = Text($"Kbd{key}");
        }

        int index = activeResources is null ? -1 : resources.MergedDictionaries.IndexOf(activeResources);
        if (index >= 0)
            resources.MergedDictionaries[index] = dictionary;
        else
            resources.MergedDictionaries.Add(dictionary);
        activeResources = dictionary;

        models.RemoveAll(reference => !reference.TryGetTarget(out _));
        foreach (var reference in models.ToArray())
            if (reference.TryGetTarget(out var model)) model.RefreshLanguage();
    }

    internal void Register(GalleryViewModel model) => models.Add(new(model));

    public string Text(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        var english = Catalog(DefaultLanguage.Tag);
        var translated = Catalog(Language.Tag);
        return translated.GetValueOrDefault(key) ?? english.GetValueOrDefault(key) ?? key;
    }

    public string Render(GalleryMessage? message)
    {
        if (message is null) return "";
        string format = Text(message.Key);
        if (message.Arguments.Length == 0) return format;
        var arguments = message.Arguments.Select(argument => argument is GalleryMessage text ? Render(text) : argument).ToArray();
        return string.Format(Culture, format, arguments);
    }

    private Dictionary<string, string> Catalog(string tag)
    {
        if (catalogs.TryGetValue(tag, out var catalog)) return catalog;
        var name = $"Alder.Avalonia.Gallery.Localization.{tag}.json";
        using var stream = typeof(GalleryLocalization).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing gallery language {tag}");
        catalog = JsonSerializer.Deserialize(stream, GalleryJsonContext.Default.Catalog) ?? new();
        catalogs.Add(tag, catalog);
        return catalog;
    }
}

[JsonSerializable(typeof(Dictionary<string, string>), TypeInfoPropertyName = "Catalog")]
internal sealed partial class GalleryJsonContext : JsonSerializerContext;
