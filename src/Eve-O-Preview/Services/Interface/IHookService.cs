using System;
using System.Threading.Tasks;

namespace EveOPreview.Services.Interface
{
    public interface IHookService
    {
        bool Ping(IntPtr handle);

        Task TellEveClientFocusIsComingAsync(IntPtr hPtr);
        
        Task TellEveClientFocusIsMaybeComingSoonAsync(IntPtr handle, int timeoutMs = 5000);

        Task<bool> UpdateTargetFpsAsync(IntPtr handle);

        Task TryInstallHooksAsync(IProcessInfo procInfo);

        Task<bool> DisableFpsLimiterAsync(IntPtr handle);

        Task<bool> UpdateMutedAudioAsync(IntPtr handle);
    }
}