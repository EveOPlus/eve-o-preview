# Working in EVE-O Preview

This directory is `src/` of the Git repository. Start with [README.md](README.md), then read the relevant guide before changing behavior. Treat the source as authoritative if a guide drifts. These instructions describe project constraints; they do not override the user's requested scope or require approval for ordinary work.

## Find the right implementation

| Task | Read first |
| --- | --- |
| Defect investigation and regression checks | [Defect guide](docs/ai/reported-bugs.md); use its Fixed/Partial statuses, completed checklist and remaining validation before reopening a `BUG-` ID |
| Future features and implementation gaps | [Feature backlog](docs/ai/feature-backlog.md); establish scope and acceptance criteria before implementation |
| Startup, dependency injection, UI settings, profiles, MediatR | [Application and configuration](docs/ai/application-and-configuration.md) |
| Discovery, previews, z-order, hotkeys, cycling, focus, CPU affinity | [Windows and thumbnails](docs/ai/windows-and-thumbnails.md) |
| Preview graphics, native composition, platform backends | [Preview rendering](docs/ai/preview-rendering.md) and [portable contracts](Eve-O-Preview.Preview/AGENTS.md) |
| Injection, FPS, prediction, native pipe protocol, audio, crash sidecar | [Robin and native integration](docs/ai/robin.md) and [Robin instructions](Eve-O-Preview.Robin/AGENTS.md) |
| Build, publish, tests, Test Explorer, mock app | [Build and test](docs/ai/build-and-test.md) and [test instructions](tests/AGENTS.md) |
| An unfamiliar file, generated resource, or release script | [Source index](docs/ai/source-index.md) |

## Constraints that matter

- Prefer plain ASCII hyphens (`-`) instead of em dashes in UI text, documentation and responses.
- Treat committed code and UI as the stable baseline. Avoid changing them unless explicitly requested or required to complete the task; do not introduce incidental refactors, layout changes or wording churn that users must relearn with every check-in.
- Uncommitted work (staged, unstaged or untracked) is active development and may be revised freely within the feature being developed. A file with uncommitted edits may still contain established code: keep unrelated committed behavior stable and preserve other in-progress work. This distinction calls for care, not an extra approval step for already-authorized work.
- The main app is .NET 10 Windows with Avalonia desktop lifetime, workspace, thumbnail and overlay hosts. Windows adapters retain DWM images and DirectComposition graphics. The settings workspace is portable Avalonia in `Eve-O-Preview.UI`; read its local instructions when editing it. Robin is an unsafe x64 NativeAOT shared library. `Eve-O-Mock` is a separate legacy .NET Framework WPF project. Original Forms exist only as test fixtures.
- Legacy is locked to its existing feature set for legacy use only. Do not add new features, pages or controls to that theme. Continue bug fixes, compatibility work and updates to its existing features, preserving their behavior and familiar layout. New features target modern themes (Light and Dark); users are expected to migrate to a modern theme. Keep a brief notice when selecting Legacy that it may lack newer features.
- New or older/unversioned global configurations default to Dark. Preserve an explicit theme selection made after that migration, including a manual return to Legacy. This policy belongs to `ApplicationPreferences`, independently of gameplay profile versions; loading an old profile must not overwrite a later manual theme choice.
- Characters/ESI remains hidden in every theme. The modern Augments module (stable ID `Dps`) implements combat logs, alpha/DPS, location and thumbnail simulation; read [combat logs](docs/ai/combat-logs.md). Follow [the UI module handoff](Eve-O-Preview.UI/AGENTS.md#future-workspace-modules) for other modules; do not restore placeholder tabs or Legacy links.
- `Program.CreateApplicationContainerBuilder` owns Autofac registration and is shared by normal startup and isolated workspace validation. Services/configuration are shared singletons; live/static thumbnail views are created per dependency. Follow the existing view callback -> presenter -> MediatR -> service path where applicable.
- Distinguish native window handles (HWND), process handles, and process IDs. Full titles such as `EVE - Name` are persisted identities. Do not casually normalize them or rename JSON keys.
- Preserve live DWM thumbnail relationships during ordinary refresh/activation. The polling interval controls discovery and property updates, not the game's rendering FPS. `RestoreAndBringToFront` must preserve the nonactivating native window/overlay path.
- Preview z-order is maintained in MRU order and raised on dirty transitions. Repeated raises on every poll, framework `Show` during cycling, or DWM re-registration during every refresh can change focus and visual stability.
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

Follow the [repository tooling policy](../AGENTS.md): keep ad hoc scripts temporary and ignored by source control, or remove them on completion. Permanent tooling belongs in C# within the existing solution and build/test workflow.

Update the relevant guide in place when changing an invariant, protocol, feature route, or build command. Documentation must describe the current state: merge additions into the relevant sections and replace superseded text instead of appending dated updates or a running validation history. Preserve current limitations and remaining checks; Git records change history. Use stable symbol names and relative file links instead of copying large implementations or line-number inventories. Keep this entry point short; detailed explanations belong in `docs/ai/`.
