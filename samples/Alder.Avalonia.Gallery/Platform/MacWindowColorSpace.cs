using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Alder.Avalonia.Gallery.Platform;

internal static class MacWindowColorSpace
{
    public static void Apply(Window window)
    {
        if (!OperatingSystem.IsMacOS() ||
            window.TryGetPlatformHandle() is not IMacOSTopLevelPlatformHandle handle)
            return;

        // Avalonia's brushes are authored in sRGB. Tag the native surface so
        // WindowServer can convert them to the profile of the current display.
        var colorSpace = Send(GetClass("NSColorSpace"), Selector("sRGBColorSpace"));
        if (colorSpace == 0) return;

        if (handle.NSWindow != 0)
            Send(handle.NSWindow, Selector("setColorSpace:"), colorSpace);

        var layer = Send(handle.NSView, Selector("layer"));
        TagMetalLayers(layer, Send(colorSpace, Selector("CGColorSpace")), GetClass("CAMetalLayer"));
    }

    private static void TagMetalLayers(nint layer, nint colorSpace, nint metalClass)
    {
        if (layer == 0 || colorSpace == 0 || metalClass == 0) return;
        if (SendBool(layer, Selector("isKindOfClass:"), metalClass) != 0)
        {
            Send(layer, Selector("setColorspace:"), colorSpace);
            return;
        }

        var children = Send(layer, Selector("sublayers"));
        var count = (nuint)Send(children, Selector("count"));
        for (nuint index = 0; index < count; index++)
            TagMetalLayers(SendAtIndex(children, Selector("objectAtIndex:"), index), colorSpace, metalClass);
    }

    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    [DllImport(ObjectiveC, EntryPoint = "objc_getClass")]
    private static extern nint GetClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC, EntryPoint = "sel_registerName")]
    private static extern nint Selector([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern void Send(nint receiver, nint selector, nint argument);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern byte SendBool(nint receiver, nint selector, nint argument);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern nint SendAtIndex(nint receiver, nint selector, nuint index);
}
