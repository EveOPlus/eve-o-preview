# Guidance for test changes

Read [build and test guidance](../docs/ai/build-and-test.md) and the [test README](Eve-O-Preview.Tests/README.md) before changing test infrastructure. From `src`, use `dotnet test .\tests\Eve-O-Preview.Tests\Eve-O-Preview.Tests.csproj -c Debug`; the nested application-only solution does not include these tests.

- Preserve the xUnit v3/Visual Studio adapter integration and custom entry-point dispatch in `Program.cs`. Normal launches must run xUnit; only the internal three-argument `--private-desktop` path runs a worker.
- UI/window cases use production forms and services on fresh, undisplayed STA desktops. Worker launch must use the test assembly's apphost, carry failure/output back to xUnit, and clean up desktop/process handles and temporary files.
- Follow a test's referenced production symbols when changing it. Reflection names, UI control names, and synthetic pipe bytes are coupled to the current application implementation; update them with intentional production changes without weakening the behavioral assertion.
- Keep focus assertions meaningful: establish a nonzero simulated-client active handle before comparing it, and preserve preview/overlay order and immediate activation checks. A later refresh tick is not equivalent to immediate recovery.
- Distinguish simulated DWM/audio and private-desktop coverage from actual EVE/native-hook behavior. The tests reference the application project, not Robin, and do not run application startup, install hooks, or launch the debugger sidecar.
- Add focused regression checks for changed behavior where the current harness can prove it. Describe necessary real-desktop/native/performance validation separately; never report those as covered by a passing stubbed test.
