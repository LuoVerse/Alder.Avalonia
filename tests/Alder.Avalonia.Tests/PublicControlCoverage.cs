using Alder.Avalonia.AotSmoke;
using Alder.Avalonia.Controls;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class PublicControlCoverage
{
    [Fact]
    public void NativeAotSmokeCoversEveryPublicConcreteControlAndFlyout()
    {
        var controlType = typeof(AlderButton);
        var expected = controlType.Assembly.GetExportedTypes()
            .Where(type => type.Namespace == controlType.Namespace && !type.IsAbstract &&
                (typeof(Control).IsAssignableFrom(type) || typeof(FlyoutBase).IsAssignableFrom(type)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
        var covered = PublicControlFactories.All.Select(factory => factory.Type)
            .Append(typeof(ToastHost))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
        Assert.Equal(expected, covered);
    }
}
