using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Timing; //spellward

namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Raised on the client to indicate it'd like to shoot.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestShootEvent : EntityEventArgs
{
    public NetEntity Gun;
    public GameTick ProjectileInputTick; //spellward - firing identity; never a client hit claim.
    public NetCoordinates Coordinates;
    public NetEntity? Target;
}
