using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

internal sealed class HoverPreviewTiming
{
    private static readonly ConditionalWeakTable<TopLevel, HoverPreviewTiming> Timings = new();
    private long cooldownStarted;
    private int cooldownMilliseconds;

    public static HoverPreviewTiming For(TopLevel window) => Timings.GetOrCreateValue(window);

    public (Action Close, Action? BeginExit)? Active { get; set; }
    public bool IsWarm => cooldownStarted == 0
        ? Active is not null
        : Stopwatch.GetElapsedTime(cooldownStarted).TotalMilliseconds < cooldownMilliseconds;

    public void Resume() => cooldownStarted = 0;

    public void CoolDown(int closeDelay)
    {
        if (cooldownStarted != 0) return;
        cooldownStarted = Stopwatch.GetTimestamp();
        cooldownMilliseconds = Math.Max(500, closeDelay);
    }
}
