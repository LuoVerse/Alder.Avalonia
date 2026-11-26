using Alder.Avalonia.Controls;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed record BadgeExample(AlderStatusColor Color, BadgeVariant Variant);
public sealed record BadgeExampleVariant(string Label, IReadOnlyList<BadgeExample> Badges, bool ShowDivider);
public sealed record ChipExample(string Label, AlderStatusColor Color, ChipVariant Variant, AlderSize Size);
public sealed record ChipExampleVariant(string Label, IReadOnlyList<ChipExample> Chips);
public sealed record ChipExampleSize(string Label, IReadOnlyList<ChipExampleVariant> Variants, bool ShowDivider);

public sealed class StatusIndicatorViewModel : GalleryViewModel
{
    private static readonly AlderStatusColor[] Colors =
        [AlderStatusColor.Accent, AlderStatusColor.Default, AlderStatusColor.Success, AlderStatusColor.Warning, AlderStatusColor.Danger];

    public IReadOnlyList<BadgeExampleVariant> BadgeVariants =>
        Enum.GetValues<BadgeVariant>().Select((variant, index) => new BadgeExampleVariant(Text("Status" + variant),
            Colors.Select(color => new BadgeExample(color, variant)).ToArray(), index < 2)).ToArray();

    public IReadOnlyList<ChipExampleSize> ChipSizes =>
        new[] { AlderSize.Large, AlderSize.Medium, AlderSize.Small }.Select((size, index) => new ChipExampleSize(Text(size.ToString()),
            Enum.GetValues<ChipVariant>().OrderBy(variant => variant == ChipVariant.Primary ? -1 : (int)variant)
                .Select(variant => new ChipExampleVariant(Text("Status" + variant),
                    Colors.Select(color => new ChipExample(Text("StatusLabel"), color, variant, size)).ToArray())).ToArray(), index < 2)).ToArray();
}
