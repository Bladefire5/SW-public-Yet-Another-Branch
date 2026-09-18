using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Calendar;

[Serializable, NetSerializable]
public sealed partial class CalendarBroadcastNotificationEvent : EntityEventArgs
{
    public readonly ProtoId<CalendarEventPrototype> ProtoId;

    public CalendarBroadcastNotificationEvent(ProtoId<CalendarEventPrototype> protoId)
    {
        ProtoId = protoId;
    }
}
