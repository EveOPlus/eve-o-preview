# Robin working context

Read [the injected-runtime guide](../docs/ai/robin.md) for architecture, the complete pipe contract, frame-pacing rationale, audio interception, known risks and validation boundaries. Read the root instructions as well.

- Robin runs inside the target game process as a Windows x64 Native AOT shared library. Preserve native `Initialize`, unmanaged signatures and x64 structure layouts when changing its entry points or interop.
- Pair protocol changes with `Eve-O-Preview/Services/Implementation/HookService.cs`. A responsive pipe is not proof that DXGI/audio initialization succeeded.
- Understand `DxHook.ThrottleTheFrame`, `SetOurWindowInFocus` and `PrecisionSleep` before changing timing: event-driven focus, short interruptible chunks and the final spin balance switching latency against CPU use. Disabled throttling must preserve the caller's presentation sync interval.
- Keep audio's sorted active-prefix/count invariant if retaining binary search. Changes to `VectoredHandler` must account for page-wide guard exceptions, hardware-breakpoint ownership, native callback cost, concurrent updates and game-specific exports.
- Separate intended behavior from the guide's observed risks. Avoid preserving a suspected defect merely because it is documented, or claiming comments and managed tests establish native runtime correctness.
- Validate published native changes against a fresh controlled client; already injected Robin is reused. Report any native behavior or performance measurements you could not verify. Update the guide when changing the protocol, focus/pacing rules, native ABI assumptions or lifecycle.
