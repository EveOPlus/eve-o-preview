# EVE-O Preview repository navigation

The application source and AI navigation guides are under `src/`.

Before working on this repository, read [src/AGENTS.md](src/AGENTS.md) and use [src/README.md](src/README.md) to select the relevant subsystem guide. For work below a project directory, also read its local instructions: [main app](src/Eve-O-Preview/AGENTS.md), [Robin](src/Eve-O-Preview.Robin/AGENTS.md), or [tests](src/tests/AGENTS.md).

The guides explain DWM lifetime and nonactivating preview z-order, focus prediction and CPU affinity, NativeAOT injection, FPS/audio protocol, configuration compatibility, and actual test coverage. Treat current source and the user's task as authoritative; do not preserve the guides' suspected defects as requirements.

Build/test command examples start in `src/`. For `build/`, read [the release-tooling section](src/docs/ai/build-and-test.md) before execution: Cake's lifetime hook deletes root output directories and release tasks download, sign, and package files. The root README is user-facing release documentation and includes older runtime prerequisites; current project files determine build targets.

Use [the source index](src/docs/ai/source-index.md) for the complete tracked baseline inventory. Keep instructions concise and update the relevant detailed guide when changing behavior or file locations.
