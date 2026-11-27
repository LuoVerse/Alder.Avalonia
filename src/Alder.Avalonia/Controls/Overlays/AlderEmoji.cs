using System.Globalization;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>The default or one of the five Unicode emoji skin tones.</summary>
public enum AlderEmojiSkinTone
{
    /// <summary>Displays the unmodified emoji.</summary>
    Default,
    /// <summary>Displays the light variant.</summary>
    Light,
    /// <summary>Displays the medium-light variant.</summary>
    MediumLight,
    /// <summary>Displays the medium variant.</summary>
    Medium,
    /// <summary>Displays the medium-dark variant.</summary>
    MediumDark,
    /// <summary>Displays the dark variant.</summary>
    Dark
}

/// <summary>An emoji and the accessible name and keywords used to find it.</summary>
public sealed class AlderEmoji
{
    private readonly string? localizedName;
    private readonly string? localizedKeywords;
    private readonly string[] variants;

    /// <summary>Creates an emoji entry for a custom category.</summary>
    public AlderEmoji(string glyph, string name, string? keywords = null)
        : this(glyph, name, keywords, null, null, []) { }

    internal AlderEmoji(string glyph, string name, string? keywords, string? localizedName,
        string? localizedKeywords, string[] variants)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(glyph);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Glyph = glyph;
        Name = name;
        Keywords = keywords;
        this.localizedName = localizedName;
        this.localizedKeywords = localizedKeywords;
        this.variants = variants;
    }

    /// <summary>The unmodified emoji, including its presentation selectors.</summary>
    public string Glyph { get; }

    /// <summary>The default short name of the emoji.</summary>
    public string Name { get; }

    /// <summary>Additional search terms for the emoji.</summary>
    public string? Keywords { get; }

    /// <summary>Gets the matching Unicode variant or the original emoji when no variant exists.</summary>
    public string GetGlyph(AlderEmojiSkinTone tone)
    {
        var index = (int)tone - 1;
        return index >= 0 && index < variants.Length && variants[index].Length != 0 ? variants[index] : Glyph;
    }

    /// <summary>Gets the display name for the current UI culture.</summary>
    public string GetDisplayName() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" && !string.IsNullOrEmpty(localizedName)
            ? localizedName : Name;

    internal bool HasGlyph(string glyph) => Glyph == glyph || Array.IndexOf(variants, glyph) >= 0;

    internal bool Matches(string term)
    {
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        var comparer = CultureInfo.InvariantCulture.CompareInfo;
        return Glyph.Contains(term, StringComparison.Ordinal) ||
            Array.Exists(variants, glyph => glyph.Contains(term, StringComparison.Ordinal)) ||
            Search(Name) || Search(Keywords) || Search(localizedName) || Search(localizedKeywords);

        bool Search(string? value) => value is not null && comparer.IndexOf(value, term, options) >= 0;
    }
}

/// <summary>A labeled section of emoji in the picker.</summary>
/// <param name="Key">A stable, unique key used for category navigation.</param>
/// <param name="Title">The accessible category label.</param>
/// <param name="Icon">The emoji, geometry, or icon control shown in the category selector.</param>
/// <param name="Items">The emoji in their display order.</param>
public sealed record AlderEmojiCategory(string Key, string Title, object? Icon, IReadOnlyList<AlderEmoji> Items)
{
    /// <summary>Optional outline geometry displayed in place of the emoji icon.</summary>
    public Geometry? IconData { get; init; }

    internal string? ResourceKey { get; init; }
}

internal static class AlderEmojiCatalog
{
    private static readonly (string Key, string Icon)[] Sections =
    [
        ("Smileys", "😀"), ("People", "👋"), ("Animals", "🐶"), ("Food", "🍎"),
        ("Travel", "🚗"), ("Activities", "⚽"), ("Objects", "💡"), ("Symbols", "🔣"), ("Flags", "🏁")
    ];

    internal static readonly Lazy<IReadOnlyList<AlderEmojiCategory>> Categories = new(Load);

    private static IReadOnlyList<AlderEmojiCategory> Load()
    {
        using var stream = typeof(AlderEmojiCatalog).Assembly.GetManifestResourceStream("Alder.Avalonia.EmojiCatalog.tsv")
            ?? throw new InvalidOperationException("The embedded emoji catalog is missing.");
        using var reader = new StreamReader(stream);
        var categories = new List<AlderEmojiCategory>(Sections.Length);
        List<AlderEmoji> items = [];
        string? title = null;
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith('>'))
            {
                AddCategory();
                title = line[1..];
                items = [];
                continue;
            }
            var fields = line.Split('\t');
            items.Add(new AlderEmoji(fields[0], fields[1], Field(2), Field(3), Field(4), Field(5)?.Split('|') ?? []));
            string? Field(int index) => index < fields.Length && fields[index].Length > 0 ? fields[index] : null;
        }
        AddCategory();
        // Category navigation places activities before travel.
        (categories[4], categories[5]) = (categories[5], categories[4]);
        return categories.AsReadOnly();

        void AddCategory()
        {
            if (title is null) return;
            var (key, icon) = Sections[categories.Count];
            categories.Add(new(key, title, icon, items.AsReadOnly()) { ResourceKey = $"AlderText.Emoji{key}" });
        }
    }
}
