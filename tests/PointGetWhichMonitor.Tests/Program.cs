using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SevenKeyboard;

internal static partial class Program
{
    private static int _checks;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--require-aot"))
                Check(!RuntimeFeature.IsDynamicCodeSupported, "NativeAOT execution");
            ExplicitPointAndSnapshot();
            CursorFallback();
            NativeReadOnly();
            Console.WriteLine($"PASS: {_checks} checks; {RuntimeInformation.ProcessArchitecture}; dynamic={RuntimeFeature.IsDynamicCodeSupported}");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static DisplayMonitorInfo Info(nint handle) =>
        new(handle, 0, "", new(0, 0, 100, 100), new(0, 0, 100, 90));

    private static void ExplicitPointAndSnapshot()
    {
        var calls = new List<string>();
        IReadOnlyList<DisplayMonitorInfo> snapshot = [Info(11), Info(22), Info(22)];
        nint selected = 22;
        var query = new PointMonitorQuery(
            () => throw new InvalidOperationException("Explicit coordinates must not read the cursor."),
            (x, y, flags) =>
            {
                Check(x == int.MinValue && y == int.MaxValue, "signed coordinate boundaries preserved");
                Check(flags == uint.MaxValue, "unknown flag bits passed unchanged");
                calls.Add("native");
                return selected;
            },
            () => { calls.Add("snapshot"); return snapshot; });

        Check(query.Get(int.MinValue, int.MaxValue, uint.MaxValue) == 2, "first duplicate match, one-based");
        Check(calls.SequenceEqual(new[] { "native", "snapshot" }), "native query precedes snapshot");
        snapshot = [Info(22), Info(11)];
        Check(query.Get(int.MinValue, int.MaxValue, uint.MaxValue) == 1, "current snapshot order used each time");
        snapshot = [Info(11)];
        Check(query.Get(int.MinValue, int.MaxValue, uint.MaxValue) == 0, "unmatched native handle has no fallback");
        selected = 0;
        calls.Clear();
        Check(query.Get(int.MinValue, int.MaxValue, uint.MaxValue) == 0, "zero native handle absent from snapshot");
        Check(calls.SequenceEqual(new[] { "native", "snapshot" }), "zero native result still reads snapshot");
        snapshot = [];
        Check(query.Get(int.MinValue, int.MaxValue, uint.MaxValue) == 0, "empty snapshot");

        var expected = new InvalidOperationException("snapshot failed");
        var failing = new PointMonitorQuery(
            () => null, (_, _, _) => 0, () => throw expected);
        bool propagated = false;
        try { failing.Get(0, 0); }
        catch (InvalidOperationException error) when (ReferenceEquals(error, expected)) { propagated = true; }
        Check(propagated, "snapshot failure is not converted to absence");

        var ordered = new PointMonitorQuery(
            () => null,
            (_, _, _) => { snapshot = [Info(44)]; return 44; },
            () => snapshot);
        Check(ordered.Get(0, 0) == 1, "snapshot is obtained after native selection");
    }

    private static void CursorFallback()
    {
        foreach (var coordinates in new (int? X, int? Y)[] { (null, null), (999, null), (null, -999) })
        foreach (uint flags in new uint[] { 0, 1, 2, uint.MaxValue })
        {
            var calls = new List<string>();
            var query = new PointMonitorQuery(
                () => { calls.Add("cursor"); return (-123, 456); },
                (x, y, actualFlags) =>
                {
                    Check(x == -123 && y == 456, "either missing coordinate replaces both with cursor");
                    Check(actualFlags == flags, "cursor path preserves flags");
                    calls.Add("native");
                    return 77;
                },
                () => { calls.Add("snapshot"); return new[] { Info(77) }; });
            Check(query.Get(coordinates.X, coordinates.Y, flags) == 1, "cursor result maps to snapshot");
            Check(calls.SequenceEqual(new[] { "cursor", "native", "snapshot" }), "one cursor read before selection");
        }

        var failedCursor = new PointMonitorQuery(
            () => null,
            (_, _, _) => throw new InvalidOperationException("Native selection after failed cursor."),
            () => throw new InvalidOperationException("Snapshot after failed cursor."));
        Check(failedCursor.Get() == 0, "failed cursor query returns zero immediately");
        Check(failedCursor.Get(123) == 0, "partial coordinates do not survive failed cursor query");
    }

    private static void NativeReadOnly()
    {
        nint dpi = GetThreadDpiAwarenessContext();
        var snapshot = MonitorExGetUtils.GetInfoList();
        Check(snapshot.Count > 0, "native monitor snapshot available");
        var points = new List<(int X, int Y)> { (int.MinValue, int.MaxValue), (0, 0) };
        foreach (var info in snapshot)
            points.Add(((int)(((long)info.Monitor.Left + info.Monitor.Right) / 2),
                (int)(((long)info.Monitor.Top + info.Monitor.Bottom) / 2)));

        foreach (var point in points)
        foreach (uint flags in new uint[] { 0, 1, 2 })
        {
            // Independently pack POINT to check the library's struct-by-value ABI on x86 and x64.
            long packed = unchecked((long)((ulong)(uint)point.X | ((ulong)(uint)point.Y << 32)));
            nint handle = MonitorFromPoint(packed, flags);
            int expected = 0;
            for (int i = 0; i < snapshot.Count; i++)
                if (snapshot[i].HMonitor == handle) { expected = i + 1; break; }
            Check(PointGetWhichMonitor.Get(point.X, point.Y, flags) == expected,
                $"native point {point.X},{point.Y}; flags={flags}");
        }

        // The user may move the cursor between calls; only check each result's valid range.
        foreach (int index in new[] { PointGetWhichMonitor.Get(), PointGetWhichMonitor.Get(x: int.MinValue),
            PointGetWhichMonitor.Get(y: int.MaxValue) })
            Check(index >= 0 && index <= snapshot.Count, "public cursor lookup returns a valid index or zero");
        Check(ReferenceEquals(snapshot, MonitorExGetUtils.GetInfoList()), "lookups do not force a cache refresh");
        Check(GetThreadDpiAwarenessContext() == dpi, "caller DPI context preserved");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        _checks++;
    }

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint MonitorFromPoint(long point, uint flags);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint GetThreadDpiAwarenessContext();
}
