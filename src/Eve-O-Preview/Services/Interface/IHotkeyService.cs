using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EveOPreview.Services;

// Platform-neutral boundary: shortcut names retain the profile's existing string format.
// Native key codes, hook handles and message pumps belong entirely to the platform adapter.
public enum HotkeyMode { Global, OperatingSystem }
public sealed record HotkeyBinding(string Shortcut, Action Execute, bool OnRelease = false);

public interface IHotkeyService : IDisposable
{
    string RegistrationWarning { get; }
    // Keep display templates separate from raw shortcut names for host localization.
    IReadOnlyList<FormattableString> RegistrationWarnings => Array.Empty<FormattableString>();
    // Replaces the complete set, releasing old registrations first. Execute runs on the UI owner.
    // Diagnostic passthrough applies to Global only, forwards original input before queuing actions,
    // and never relaxes capture suppression. It cannot acknowledge target application processing.
    void Replace(IReadOnlyList<HotkeyBinding> bindings, HotkeyMode mode, bool diagnosticPassthrough = false);
    // Suspends ALL actions/registrations until capture ends, then restores the latest set.
    // null means Escape; timeout and cancellation retain their standard exceptions.
    Task<string> CaptureAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
