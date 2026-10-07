# Portable preview contracts

Read [the source instructions](../AGENTS.md) and [preview rendering guide](../docs/ai/preview-rendering.md).

- Keep this project independent of UI toolkits, Windows handles, System.Drawing, capture buffers and filesystem operations.
- Windows is the primary platform. Backends report supported capabilities independently; Linux limitations must not remove Windows features or require extra frame copies.
- `IPreviewSession` represents an image-presentation lifetime, including DWM's native relationship. Do not require a frame stream for native presentation.
- Overlay state uses preview-client pixels and ARGB colors. Preserve the existing title font's drawing units and full-title persistence keys in host adapters.
- Renderer calls run on the owning UI thread. Immutable scenes and finite alerts cross the boundary; frame callbacks must not call configuration, telemetry or IPC.
