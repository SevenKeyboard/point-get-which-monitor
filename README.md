# PointGetWhichMonitor

C# port of pointGetWhichMonitor 1.0.0. Targets `net10.0-windows`; supports NativeAOT.

```csharp
using SevenKeyboard;

int cursorMonitor = PointGetWhichMonitor.Get();
int pointMonitor = PointGetWhichMonitor.Get(-100, 200);
int nearestMonitor = PointGetWhichMonitor.Get(-100, 200, flags: 2);
```

Returns the first matching **one-based** monitor index in the shared snapshot, or 0.
If either coordinate is omitted, both come from GetCursorPos; its failure returns 0.
Numeric flags pass unchanged to MonitorFromPoint (0=null, 1=primary, 2=nearest).
The caller's DPI context and the existing monitor-cache refresh policy are preserved.
Snapshot errors propagate. AHK flag-name strings are represented by numeric flags.

References the sibling MonitorExGetUtils project (minimum 2.0.0, same major) and
imports `library-build/main-cs/CheckLibraryVersions.targets`. Override
`MonitorExGetUtilsProjectPath` and `LibraryBuildTargetsPath` for another layout;
the dependency also accepts `DisplayMonitorInfoManagerProjectPath`.
No dependency sources are included.

From this folder in PowerShell:

```powershell
dotnet build tests/PointGetWhichMonitor.Tests -c Release -warnaserror -p:PlatformTarget=x64 --artifacts-path .artifacts/x64
dotnet .artifacts/x64/bin/PointGetWhichMonitor.Tests/release/PointGetWhichMonitor.Tests.dll
```

For x86 use `PlatformTarget=x86`, a separate artifacts path, and the x86 dotnet host.
For NativeAOT use `dotnet publish` with `-r win-x64` or `win-x86`,
`-p:PublishAot=true`, and matching `PlatformTarget`; run the executable with
`--require-aot` after any signing required by the host.

Tests cover cursor fallback/failure, raw flags, signed coordinate boundaries,
snapshot ordering/absence/errors, and read-only native queries. They send no input
and change no system settings.
