namespace EveOPreview.Preview;

/// <summary>Session identity; adapters map this to native resources. Never persisted in place of full title keys.</summary>
public readonly record struct PreviewClientId(long Value);

[Flags]
public enum PreviewCapabilities
{
    None = 0,
    NativeLiveImage = 1,
    CapturedImage = 2,
    ImageTransforms = 4,
}

/// <summary>
/// A presentation lifetime, not an obligatory stream of captured frames. UI-thread calls;
/// the platform adapter owns source/destination handles, image buffers and recovery.
/// </summary>
public interface IPreviewSession : IDisposable
{
    PreviewCapabilities Capabilities { get; }
    void SetBounds(PreviewRect bounds);
    void Refresh(bool maintenance);
}

public interface IPreviewBackend
{
    PreviewCapabilities Capabilities { get; }
    IPreviewSession CreateSession(PreviewClientId client);
}
