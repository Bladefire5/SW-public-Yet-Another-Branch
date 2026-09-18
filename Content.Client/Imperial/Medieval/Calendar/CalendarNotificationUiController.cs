using Content.Shared.Imperial.Medieval.AreaMarker;
using Content.Shared.Imperial.Medieval.Calendar;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Calendar;

public sealed class CalendarNotificationUiController : UIController
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;

    public event Action<string, string>? BroadcastReceived;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CalendarBroadcastNotificationEvent>(OnBroadcastNotification);
    }

    private void OnBroadcastNotification(CalendarBroadcastNotificationEvent ev, EntitySessionEventArgs args)
    {
        if (!_prototype.TryIndex(ev.ProtoId, out CalendarEventPrototype? proto))
            return;

        var text = !string.IsNullOrEmpty(proto.Name) ? Loc.GetString("wrapped-area-marker-message", ("area", Loc.GetString(proto.Name)), ("fontSize", 24)) : string.Empty;

        var even = new AreaMarkerAnnounceEvent(text);
        _entManager.EventBus.RaiseEvent(EventSource.Local, ref even);

        BroadcastReceived?.Invoke(proto.Texture, text);

        if (proto.Sound != null)
        {
            var audio = _entManager.System<SharedAudioSystem>();
            audio.PlayGlobal(proto.Sound, Filter.Local(), false);
        }
    }
}
