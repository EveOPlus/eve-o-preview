# EVE-O Preview repository navigation

The application source and AI navigation guides are under `src/`.

Before working on this repository, read [src/AGENTS.md](src/AGENTS.md) and use [src/README.md](src/README.md) to select the relevant subsystem guide. For work below a project directory, also read its local instructions: [main app](src/Eve-O-Preview/AGENTS.md), [Robin](src/Eve-O-Preview.Robin/AGENTS.md), or [tests](src/tests/AGENTS.md).

The guides explain DWM lifetime and nonactivating preview z-order, focus prediction and CPU affinity, NativeAOT injection, FPS/audio protocol, configuration compatibility, and actual test coverage. Treat current source and the user's task as authoritative; do not preserve the guides' suspected defects as requirements.

Build/test command examples start in `src/`. For `build/`, read [the release-tooling section](src/docs/ai/build-and-test.md) before execution: Cake's lifetime hook deletes root output directories and release tasks download, sign, and package files. The root README is user-facing release documentation and includes older runtime prerequisites; current project files determine build targets.

Use [the source index](src/docs/ai/source-index.md) for the source and subsystem map. Keep instructions concise and update the relevant detailed guide when changing behavior or file locations.

Maintain Markdown files and other documentation as a single account of the current state. Update the relevant sections in place, merging useful additions and removing superseded or contradictory wording. Do not append dated edits, follow-up notes or validation runs as a running history; Git records change history. Keep current validation scope, known limitations and remaining work together in their relevant sections.

Do not commit ad hoc automation or standalone maintenance scripts. Keep task-specific scripts temporary in source-control-ignored locations such as `.ai-work/` or `src/bin/`, or remove them when the task is complete. Implement tooling intended for ongoing use in C#, integrated into the existing solution and its build/test workflow; reuse established project infrastructure where possible.
