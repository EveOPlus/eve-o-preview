using System.Windows.Forms;

namespace EveOPreview.Mediator.Messages
{
    public class CaptureNewHotkeyResponse
    {
        public bool IsValid { get; set; }
        public Keys KeysCaptured { get; set; }
        public string KeyString { get; set; }
        public string ErrorMessage { get; set; }
    }
}