# Main application navigation

Read [the source instructions](../AGENTS.md) and the relevant [guide](../README.md). This directory contains the host UI and services, not Robin's in-process implementation.

- The default settings view is `WorkspaceForm`, hosting portable `Eve-O-Preview.UI` controls. Trace `WindowsWorkspaceBackend`, `IMainFormView`/`IAsyncSettingsView`, `MainFormPresenter`, configuration and the corresponding handler. The former `MainForm` and designer remain for regression coverage. FPS/audio settings and cycle groups are shared references; most other values are copied explicitly.
- Preserve `_suppressEvents`, `_suppressSizeNotifications`, and the feedback path from thumbnail resize back to the main form. Inspect callback timing before changing synchronous waits or `async void` event boundaries.
- Before changing previews, focus, z-order, or CPU affinity, read [windows-and-thumbnails.md](../docs/ai/windows-and-thumbnails.md). Before changing `HookService` or `DebuggerSidecar`, read [robin.md](../docs/ai/robin.md).
- Keep JSON compatibility and title-based layout keys in view. `AppConfig.ConfigFileName` is not the active profile resolver; `ProfileManager` and `ConfigurationStorage.CurrentProfile` are.
- Maintain designer/control event wiring when changing the UI. `AboutBox` is excluded from compilation in the project file. The misspelled `Excpetions` folder and `Minimise*.cs` filenames are existing paths, not alternate features.
- `Program` is the composition root. Do not assume an interface is registered just because a matching class exists; some handlers deliberately request the concrete `MainFormPresenter`.
- For log parsing, read [log-languages.md](../docs/ai/log-languages.md), including its [real-client sample gaps](../docs/ai/log-languages.md#real-client-sample-gaps). Support only the eight listed game languages. Client templates and recorded client output override assumptions; keep markup cleanup shared across event types. Expand coverage beyond combat, and add anonymized, self-contained fixtures when real examples become available. Do not mark a gap verified from generated template tests alone or document private input paths.
- Every supported log language must accept the client's **Important Names in English** option. Keep message-language selection separate from NPC/item/ore/station/system name lookup. Resolve visible names through SDE aliases regardless of language; localized hints may be in the opposite language and must never override visible text.

See [application-and-configuration.md](../docs/ai/application-and-configuration.md) for routes and known investigation points, and [build-and-test.md](../docs/ai/build-and-test.md) for targeted validation.
