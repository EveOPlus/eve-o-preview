# Optional native integration check

Requires Windows x64, .NET 10, the Visual Studio C++ toolchain/Windows SDK, and a hardware D3D11 device. Run from `src` in an x64 Developer PowerShell. This is separate from the ordinary xUnit suite because it compiles native code and injects a DLL into a controlled child process.

```powershell
./tests/Robin.NativeSmoke/build-probe.ps1
dotnet publish ./Eve-O-Preview.Robin/Eve-O-Preview.Robin.csproj -c Release -r win-x64 -p:IlcUseEnvironmentalTools=true -o ./bin/robin-native-check
if ($LASTEXITCODE -ne 0) { throw 'Native publish failed' }
dotnet build ./tests/Robin.NativeSmoke/Robin.NativeSmoke.csproj -c Debug
if ($LASTEXITCODE -ne 0) { throw 'Smoke driver build failed' }
./tests/Robin.NativeSmoke/bin/Debug/net10.0-windows/Robin.NativeSmoke.exe ./bin/robin-native-check/Eve-O-Preview.Robin.dll ./tests/Robin.NativeSmoke/bin/native/probe.exe
```

`IlcUseEnvironmentalTools=true` uses the loaded VS compiler environment, useful when SDK tool discovery cannot find vswhere. Check every exit code: a failed publish can otherwise leave an old DLL at the output path. The smoke output prints the loaded version and exact SHA-256. Restore again if an IDE build replaces the assets file without the win-x64 target. Cake is not needed.

The driver launches its own off-screen, nonactivating DXGI window, uses the application's production HookService to inject Robin, and always closes its child. It tests:

- Published AOT identity and native Present/audio readiness.
- Atomic mute configuration, malformed/truncated/oversized requests, more than 100 disconnected peers, and recovery from a stalled connection.
- Actual D3D11 Present/Present1 pacing, nested-presentation protection, TEST bypass, interrupting a 1 FPS wait by disabling, and eight timed production pipe wakes while all three targets are 1 FPS.
- Synthetic Wwise-compatible exports, nested/repeated PostEvent return handling, the independently pinned SDK Stop=0 action, full 64-bit diagnostic game objects and forwarding another guard page.
- Owner exit with FPS disabled/no rendering, shutdown cleanup, and replacing the installation DLL while the hash-addressed copy remains loaded.
- Separate warmed allocation checks compiling the production source in managed form: 256 frame waits and 100,000 audio lookups must allocate zero bytes. These do **not** measure complete NativeAOT VEH allocations or GC pauses.

The synthetic audio DLL uses `/NOENTRY`: it has no CRT loader callback on its compact export page. A fixture with CRT thread-detach code on that page exposed a NativeAOT reentry failure; production now refuses an export page shared with the DLL entry point. This is a guard-page limitation, not a guarantee about arbitrary loader callbacks. The fixture does not reproduce EVE's whole sound engine or PAGE_GUARD concurrency races.

To exercise production injection/lifecycle in Eve-O-Mock instead:

```powershell
./tests/Robin.NativeSmoke/bin/Debug/net10.0-windows/Robin.NativeSmoke.exe ./bin/robin-native-check/Eve-O-Preview.Robin.dll ./Eve-O-Mock/bin/Debug/ExeFile.exe
```

This starts a fresh Mock and closes only that child. Its HelixToolkit rendering is not used as a DXGI Present pacing assertion.

For an interactive-desktop focus check with four owned Mock instances:

```powershell
./tests/Robin.NativeSmoke/bin/Debug/net10.0-windows/Robin.NativeSmoke.exe ./bin/robin-native-check/Eve-O-Preview.Robin.dll --focus-mock ./Eve-O-Mock/bin/Debug/ExeFile.exe
```

This changes foreground between those four test windows for 16 cycles at 60/1/1 FPS, checks actual foreground and message processing, clears native settings, then closes only its own children. Run on the input desktop when the user is ready for focus changes. It does not synthesize a hotkey or validate EVE-specific input handling.

For deliberate interactive EVE diagnostics, `--client PID [eventId]` requires a fresh, uninjected EVE client on the same desktop. It injects the provided DLL, verifies its hash, captures 15 seconds of recent events, optionally mutes the supplied event for 20 seconds, then clears FPS, muting and capture. It does not close EVE. The DLL remains loaded; restart that client to change binaries. Logs and event JSON stay in the ignored driver output directory. Use this mode only with the user ready to trigger/listen to the event; do not replace another running host's native settings.
