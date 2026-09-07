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
  and image capture, applies the border before activation without a UI continuation, preserves focus,
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

The defect-investigation additions exercise full profile load/edit/save/rename/clone
workflows, live settings propagation and current factory configuration, production
hotkey subscriptions with pending affinity and immediate borders/focus, and process/GDI/affinity lifetime
in isolated workers. Pipe tests cover both old/new audio protocols and response
timeouts. These additions established a baseline of 50 cases after theory expansion.

The modern workspace additions bring the suite to 86 expanded cases. They cover
global preference persistence and portable/fallback locations, preservation of
future global settings, theme validation, actual profile-file accent round trips,
backend input validation, awaited saves before FPS/audio application, retry after
a failed save, individual visibility while Hide All is active, and cycling edits
that retain full titles and extra shortcuts. A private desktop case initializes
the production WinForms/Avalonia host, changes themes and pages, and exercises
hide/show and disposal without starting the presenter or native services. It
also establishes a nonzero active simulated client before refreshing an
inactive workspace, verifies native active/foreground handles remain unchanged,
and checks that close can cancel or discard unapplied edits.

Three visual cases capture the original MainForm tabs and control metrics,
compare preview pixels against actual native title/highlight rendering, and
capture the production Windows workspace. The title editor case types decimal
font/outline sizes, blurs the fields, verifies staged font/color/offset/highlight
edits in the displayed image and grouped Apply, and requires at least 200 pixels
of editor viewport at the minimum modern window size while the preview remains
pinned. The host case checks Legacy's 460 by 417 client size and restores the
modern window size after switching themes.

The PNG captures and original control metrics are written beside the test
executable in `original-ui`, `title-preview`, and `native-ui`. Title pixel
comparisons invoke the actual `OutlinedLabel` instance in `ThumbnailOverlay`;
`DrawToBitmap` omits that layered window's label, so it is unsuitable as a title
reference capture. The highlight reference uses production `ThumbnailView`
insets with a solid image fixture. These checks do not capture a live EVE client.

The separate [portable visual smoke executable](../Eve-O-Preview.UI.Smoke/README.md)
renders the actual Avalonia controls in all three themes and checks navigation,
search, draft retention, validation, profile accent cues and compact layouts.

`WorkspaceCompositionTests` resolves the workspace from the production Autofac
registrations with real configuration services and an isolated profile root on a
private desktop. It does not start native services. In-process composition cannot
establish which DLL a standalone build or single-file bundle will load: Cake also
runs the host's `--validate-workspace` mode from a directory containing only the
published executable, exercising UI resources and native rendering dependencies.

`CharacterPortraitCacheTests` checks deduplicated downloads, character-ID filenames,
reuse after restarting, weekly background refresh, and retention/backoff on failed
or invalid image responses. Requests use a controlled HTTP handler and the shared
test-only JPEG fixture. `WorkspaceHostTests` also opens and closes the donation
details inside the real Windows/Avalonia host across all three themes.

The `workspace-dpi` private-desktop case sends synthetic 100/125/150/200% DPI
transitions through the production host and checks render scale, child bounds,
focus/draft retention, actual-pixel sample sizing and Legacy/modern size restoration.
It also moves its own window between available monitors and reports their DPI.
Synthetic messages cannot change native caption/border metrics and do not replace
manual testing on monitors with genuinely different scaling.

Actual NativeAOT injection and DXGI/synthetic-audio checks are an optional separate
[Robin.NativeSmoke](../Robin.NativeSmoke/README.md) run. They do not run implicitly
from Test Explorer and have their own native toolchain/GPU prerequisites.
