using Avalonia.Input;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class LayoutText(string key, int count = 0, int number = 0) : GalleryViewModel
{
    public string Label => number > 0 ? Format(key, number) : Text(key);
    public IEnumerable<string> Rows => Enumerable.Range(1, count).Select(index => Format("NavDemoSectionItem", Label, index));
}

public sealed class LayoutMetric(string key, string value, string change) : GalleryViewModel
{
    public string Label => Text(key);
    public string Value { get; } = value;
    public string Change { get; } = change;
}

public sealed class LayoutEpisode(string key, string guest, string plays, string dateKey) : GalleryViewModel
{
    public string Title => Text(key);
    public string Guest { get; } = guest;
    public string Plays { get; } = plays;
    public string Date => Text(dateKey);
}

public sealed class LayoutMessage(string key, bool isReply = false) : GalleryViewModel
{
    public string Body => Text(key);
    public bool IsReply { get; } = isReply;
}

public static class LayoutSamples
{
    public static KeyGesture AsideShortcut { get; } = new(Key.OemPeriod,
        OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control);
    public static IReadOnlyList<LayoutMetric> Metrics { get; } =
    [
        new("NavDemoMonthlyPlays", "84,210", "+14%"),
        new("NavDemoActiveSubscribers", "3,487", "+6%"),
        new("NavDemoEpisodesPublished", "48", "+2")
    ];

    public static IReadOnlyList<LayoutEpisode> Episodes { get; } =
    [
        new("NavDemoDesigningForAttentionSpans", "Ada Park", "12,489", "NavDemoTwoDays"),
        new("NavDemoThePricingConversationNobodyWants", "Myles Silva", "8,902", "NavDemoFiveDays"),
        new("NavDemoBuildingACreativeStudioFromZero", "Tessa Huang", "21,008", "NavDemoAWeek"),
        new("NavDemoLateNightDebuggingWithAda", "Ada Park", "4,213", "NavDemoTwoWeeks")
    ];

    public static IReadOnlyList<LayoutText> Features { get; } =
    [
        new("NavDemoAccessibleControls"), new("NavDemoThemeTokens"), new("NavDemoComposableTemplates"),
        new("NavDemoLightAndDarkThemes")
    ];

    public static IReadOnlyList<LayoutText> ScrollGroups { get; } =
    [
        new("NavDemoOverview", 4), new("NavDemoActivity", 4), new("NavDemoTimeline", 4),
        new("NavDemoMetrics", 4), new("NavDemoNotes", 4)
    ];

    public static IReadOnlyList<LayoutText> ToolbarRows { get; } =
        Enumerable.Range(1, 12).Select(index => new LayoutText("NavDemoItemNumber", number: index)).ToArray();

    public static IReadOnlyList<LayoutMessage> Messages { get; } =
    [
        new("NavDemoMessageDeposit"), new("NavDemoMessageFollowup", true), new("NavDemoMessageThanks"),
        new("NavDemoMessageResolved", true)
    ];
}
