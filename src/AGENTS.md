# Working in EVE-O Preview

This directory is `src/` of the Git repository. Start with [README.md](README.md), then read the relevant guide before changing behavior. Treat the source as authoritative if a guide drifts. These instructions describe project constraints; they do not override the user's requested scope or require approval for ordinary work.

## Find the right implementation

| Task | Read first |
| --- | --- |
| Defect investigation and regression checks | [Defect guide](docs/ai/reported-bugs.md); follow `BUG-` IDs and verify runtime hypotheses against current source |
| Future features and implementation gaps | [Feature backlog](docs/ai/feature-backlog.md); establish scope and acceptance criteria before implementation |
| Startup, dependency injection, UI settings, profiles, MediatR | [Application and configuration](docs/ai/application-and-configuration.md) |
| Discovery, previews, z-order, hotkeys, cycling, focus, CPU affinity | [Windows and thumbnails](docs/ai/windows-and-thumbnails.md) |
| Injection, FPS, prediction, native pipe protocol, audio, crash sidecar | [Robin and native integration](docs/ai/robin.md) and [Robin instructions](Eve-O-Preview.Robin/AGENTS.md) |
| Build, publish, tests, Test Explorer, mock app | [Build and test](docs/ai/build-and-test.md) and [test instructions](tests/AGENTS.md) |
| An unfamiliar file, generated resource, or release script | [Source index](docs/ai/source-index.md) |

## Constraints that matter

- The main app is .NET 10 Windows WinForms (also enables WPF); Robin is an unsafe x64 NativeAOT shared library. `Eve-O-Mock` is a separate legacy .NET Framework WPF project. Check project files before adopting root README prerequisites or selecting a solution-wide build.
- `Program.InitializeApplicationController` owns Autofac registration. Services/configuration are shared singletons; live/static thumbnail views are created per dependency. Follow the existing view callback -> presenter -> MediatR -> service path where applicable.
- Distinguish native window handles (HWND), process handles, and process IDs. Full titles such as `EVE - Name` are persisted identities. Do not casually normalize them or rename JSON keys.
- Preserve live DWM thumbnail relationships during ordinary refresh/activation. The polling interval controls discovery and property updates, not the game's rendering FPS. `RestoreAndBringToFront` must preserve the nonactivating native window/overlay path.
- Preview z-order is maintained in MRU order and raised on dirty transitions. Repeated raises on every poll, `Form.Show` during cycling, or DWM re-registration during every refresh can change focus and visual stability.
- Cycling coordinates the selected client, the predicted next client, CPU masks, and Robin's focus state. Evaluate these together when changing switch latency.
- Read `DxHook` (`DXHook.cs`), `PrecisionSleep`, `AudioMuteSystem`, and both pipe endpoints before changing native hot paths. Timing, ABI layout, hook ordering, bounded mute data, and delegate lifetime are functional constraints. Avoid adding blocking IPC, allocations, or routine logging to frame/exception callbacks unless evidence justifies their cost.
- Configuration loads populate an existing singleton. View event-suppression and presenter size-suppression flags prevent feedback. Several settings objects are shared by reference. Trace load, edit, notification, persistence, and active-client application end to end.
- Preserve the historical mutex workaround in `Program.GetInstanceToken` unless the relevant Windows failure case is investigated. A comment explaining a workaround is evidence of intent, not proof of a measured benefit.
- Observed defects and unverified assumptions are listed separately in the guides. Do not preserve those as requirements or silently fix unrelated ones.

## Validation and maintenance

From `src/`, the focused suite is:

```powershell
dotnet test .\tests\Eve-O-Preview.Tests\Eve-O-Preview.Tests.csproj -c Debug
```

Read the build guide for prerequisites, filters, native publishing, and manual validation. Tests cover selected app behavior and isolated window operations; they do not validate real EVE injection, audio interception, frame pacing, or hybrid CPU behavior. Use `dotnet build .\Eve-O-Preview\Eve-O-Preview.csproj -c Debug` for a targeted app build. Do not use the Cake release packager as a routine check: its lifetime hook deletes repository build output directories and its tasks have download/sign/package side effects.

For documentation-only edits, check links, source references, and `git diff --check`; no application launch is needed. For behavior changes, run checks appropriate to the affected path and report what actually ran, what passed, and what remains unverified. Keep generated files, local profiles/logs, package caches, and release output out of unrelated diffs.

Update the relevant guide when changing an invariant, protocol, feature route, or build command. Use stable symbol names and relative file links instead of copying large implementations or line-number inventories. Keep this entry point short; detailed explanations belong in `docs/ai/`.
