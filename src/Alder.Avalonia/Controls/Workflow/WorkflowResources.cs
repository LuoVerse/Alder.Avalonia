using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Alder.Avalonia.Controls;

internal static class WorkflowResources
{
    public static ResourceDictionary Create()
    {
        var resources = new ResourceDictionary
        {
            ["WorkflowBranchIcon"] = Geometry.Parse(
                "M7 1 H9 V4.5 L13 8.5 V13 H15 V15 H11 V9.3 L8 6.3 L5 9.3 V15 H1 V13 H3 V8.5 L7 4.5 Z"),
            ["AlderText.SelectSeparator"] = "、",
            ["AlderText.SelectPair"] = "{0}、{1}",
            ["AlderText.SelectListEnd"] = "{0}、{1}"
        };
        var light = new ResourceDictionary();
        var dark = new ResourceDictionary();
        foreach (var (name, daylight, nighttime) in new[]
        {
            ("ShellBrush", "#F5F5F5", "#0E0E10"),
            ("CanvasBrush", "#F8F8F9", "#111113"),
            ("Dots", "#D5D5DB", "#303038"),
            ("Line", "#A7A7B0", "#6A6A77"),
            ("RejectLine", "#CB6263", "#CA7778"),
            ("BlueSurface", "#E7F3FF", "#142943"),
            ("AmberSurface", "#FFF0D6", "#392D1B"),
            ("AmberInk", "#835416", "#EAB963"),
            ("VioletSurface", "#EEE8FA", "#302540"),
            ("VioletInk", "#65449C", "#CCB0F4"),
            ("GreenSurface", "#E0F5EA", "#17332A"),
            ("GreenInk", "#246747", "#8ED3AC")
        })
        {
            light["Workflow" + name] = new SolidColorBrush(Color.Parse(daylight));
            dark["Workflow" + name] = new SolidColorBrush(Color.Parse(nighttime));
        }
        resources.ThemeDictionaries[ThemeVariant.Light] = light;
        resources.ThemeDictionaries[ThemeVariant.Dark] = dark;
        return resources;
    }
}
