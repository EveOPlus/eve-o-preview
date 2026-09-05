# EVE-O Preview tests

Requires Windows and the .NET 10 SDK.

In Visual Studio, open `EVE-O-Preview.sln`, build the test project, then open
**Test > Test Explorer** and choose **Run All**. Individual cases can also be run
from Test Explorer. Failures retain their assertion message, stack trace and
worker output in the test results, so there is no console window to copy from.

From `src`:

```powershell
dotnet test tests/Eve-O-Preview.Tests/Eve-O-Preview.Tests.csproj
```

To run just the hidden-thumbnail recovery case:

```powershell
dotnet test tests/Eve-O-Preview.Tests/Eve-O-Preview.Tests.csproj --filter "DisplayName~HiddenWindowRecovery"
```

The project uses xUnit v3 and the Visual Studio test adapter. It covers the following behaviors:

- Three legacy-profile cases cover missing, malformed and expired keys,
  preserved feature settings, and removal of obsolete licensing fields on save.
- Two FPS cases verify that enabling and disabling respect the user's setting.
- One resource check verifies that no licensing key is embedded.
- One UI case checks FPS, audio and CPU-affinity control availability.
- Custom audio cases cover ID parsing, deduplication, profile persistence,
  validation and automatic saving in the UI, and sending/clearing custom IDs
  alongside presets through a simulated audio pipe. UI renders are saved as
  `fps-audio-ui.png` and `fps-audio-invalid-ui.png` beside the test executable.
- Eleven thumbnail cases cover activation order, competing topmost windows,
  recovery after external hiding/demotion or minimizing, Hide All, individual
  and active-client hiding, focus-loss hiding, and the Always on top setting.
  Immediate-activation cases verify that reordering precedes client activation
  and image capture, survives a blocked asynchronous activation, preserves focus,
  and respects hiding settings without waiting for a refresh tick.

Live-thumbnail cases also exercise the production live view with a simulated
DWM backend: healthy refreshes and border/z-order changes retain the existing
image, while failed updates replace it only after populating the replacement.

The UI and thumbnail cases automatically launch an STA worker on a private
Windows desktop that is never displayed. Each case gets fresh windows and state.
The worker uses production forms and the thumbnail manager, with a stubbed image
renderer. It does not launch EVE, install hooks, or alter the user's windows.
Worker failures and timeouts fail their corresponding xUnit case.

Focus checks explicitly activate the simulated client on the worker thread and
compare active/foreground handles immediately before and after each operation.
They fail if the client cannot be made active; a missing active window is not
accepted as a passing baseline. The foreground check on this hidden desktop does
not replace testing foreground behavior with a live EVE client.

`Program.cs` normally delegates to xUnit, including when started from the IDE.
Its private worker entry point is used only by the automated desktop tests.
Debugging a window check's worker requires attaching to that child process;
ordinary configuration tests run directly in xUnit.

`Eve-O-Mock` remains the animated mock client for manual rendering and integration
checks. These automated checks verify window-state recovery but do not reproduce
or establish the cause of intermittent disappearance in a live EVE session.
