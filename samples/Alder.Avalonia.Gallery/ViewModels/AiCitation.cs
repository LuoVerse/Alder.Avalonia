using System.Windows.Input;
using Avalonia.Media;

namespace Alder.Avalonia.Gallery.ViewModels;

/// <summary>Source data shared by the citation chip and its interactive preview.</summary>
public sealed record AiCitation(string Title, Uri Url, IImage Logo, ICommand Open)
{
    public string Domain => Url.Host;
    public string Target => Url.AbsoluteUri;
}
