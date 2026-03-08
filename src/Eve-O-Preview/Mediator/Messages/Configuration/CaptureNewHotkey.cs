using MediatR;

namespace EveOPreview.Mediator.Messages
{
    public class CaptureNewHotkey : IRequest<CaptureNewHotkeyResponse>
    {
        public string KeysString { get; }
        public int TimeoutMs { get; }
        
        public CaptureNewHotkey(string keysString, int timeoutMs)
        {
            KeysString = keysString;
            TimeoutMs = timeoutMs;
        }
    }
}