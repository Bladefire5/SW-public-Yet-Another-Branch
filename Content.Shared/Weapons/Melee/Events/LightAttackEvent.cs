using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Melee.Events;

/// <summary>
/// Raised when a light attack is made.
/// </summary>
[Serializable, NetSerializable]
public sealed class LightAttackEvent : AttackEvent
{
    public readonly NetEntity? Target;
    public readonly NetEntity Weapon;

    // spellward start
    /// <summary>
    /// Identifies the committed swing so an old cursor update cannot retarget a later one.
    /// </summary>
    public readonly uint WindupId;
    // spellward end

    public LightAttackEvent(NetEntity? target, NetEntity weapon, NetCoordinates coordinates, uint windupId = 0) : base(coordinates) // spellward
    {
        Target = target;
        Weapon = weapon;
        WindupId = windupId; // spellward
    }
}
