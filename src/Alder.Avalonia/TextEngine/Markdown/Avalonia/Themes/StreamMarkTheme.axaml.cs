#pragma warning disable CS1591 // Imported Markdown engine.
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Alder.NativeMarkdown.UI.Themes;

/// <summary>StreamMark 默认主题。以 x:Class 方式编译加载，保证 NativeAOT 安全（不使用运行时 StyleInclude）。</summary>
public class StreamMarkTheme : Styles
{
    public StreamMarkTheme()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
