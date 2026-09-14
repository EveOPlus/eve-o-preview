using EveOPreview.Preview;

namespace EveOPreview.UI;

public static class AugmentLabels
{
    public static string Position(OverlayPosition position, Func<string, string> translate) => translate(position switch
    {
        OverlayPosition.TopLeft => "Top left", OverlayPosition.TopCenter => "Top center", OverlayPosition.TopRight => "Top right",
        OverlayPosition.MiddleLeft => "Middle left", OverlayPosition.MiddleCenter => "Center", OverlayPosition.MiddleRight => "Middle right",
        OverlayPosition.BottomLeft => "Bottom left", OverlayPosition.BottomCenter => "Bottom center", _ => "Bottom right"
    });
    public static string Order(CombatRowOrder order, Func<string, string> translate) => string.Join(" → ", (order switch
    {
        CombatRowOrder.OutgoingIncomingRepairs => new[] { "Outgoing", "Incoming", "Repairs" },
        CombatRowOrder.IncomingRepairsOutgoing => ["Incoming", "Repairs", "Outgoing"],
        CombatRowOrder.OutgoingRepairsIncoming => ["Outgoing", "Repairs", "Incoming"],
        CombatRowOrder.RepairsIncomingOutgoing => ["Repairs", "Incoming", "Outgoing"],
        CombatRowOrder.RepairsOutgoingIncoming => ["Repairs", "Outgoing", "Incoming"],
        _ => ["Incoming", "Outgoing", "Repairs"]
    }).Select(translate));
}
