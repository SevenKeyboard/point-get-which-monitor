//==============================================================
// pointGetWhichMonitor — Determine monitor index from a point
//
// GitHub: https://github.com/SevenKeyboard/point-get-which-monitor
// Author: SevenKeyboard Ltd. (2026)
// License: The Unlicense
//==============================================================

using System.Runtime.InteropServices;

namespace SevenKeyboard;

public static partial class PointGetWhichMonitor
{
    private static readonly PointMonitorQuery Query = new(
        ReadCursor, ReadMonitor, static () => MonitorExGetUtils.GetInfoList());

    /// <summary>Returns the first matching one-based monitor index, or zero.</summary>
    /// <remarks>
    /// If either coordinate is omitted, both come from the current cursor.
    /// Flags are passed unchanged to MonitorFromPoint; the default is MONITOR_DEFAULTTONULL.
    /// The caller's DPI context and the existing monitor-cache refresh policy are unchanged.
    /// </remarks>
    public static int Get(int? x = null, int? y = null, uint flags = 0) => Query.Get(x, y, flags);

    private static (int X, int Y)? ReadCursor() =>
        GetCursorPos(out Point point) != 0 ? (point.X, point.Y) : null;

    private static nint ReadMonitor(int x, int y, uint flags) =>
        MonitorFromPoint(new Point { X = x, Y = y }, flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        internal int X;
        internal int Y;
    }

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int GetCursorPos(out Point point);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint MonitorFromPoint(Point point, uint flags);
}

// Keep native selection before the snapshot lookup, including a zero native handle.
internal sealed class PointMonitorQuery(
    Func<(int X, int Y)?> readCursor,
    Func<int, int, uint, nint> monitorFromPoint,
    Func<IReadOnlyList<DisplayMonitorInfo>> readSnapshot)
{
    internal int Get(int? x = null, int? y = null, uint flags = 0)
    {
        if (!x.HasValue || !y.HasValue)
        {
            if (readCursor() is not { } point)
                return 0;
            x = point.X;
            y = point.Y;
        }

        nint monitor = monitorFromPoint(x.Value, y.Value, flags);
        IReadOnlyList<DisplayMonitorInfo> snapshot = readSnapshot();
        for (int i = 0; i < snapshot.Count; i++)
            if (snapshot[i].HMonitor == monitor)
                return i + 1;
        return 0;
    }
}
