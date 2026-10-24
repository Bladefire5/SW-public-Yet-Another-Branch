using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Poise.LightAttack;

/// <summary>
/// Changes the target and direction of an existing light wind-up, without
/// creating another swing or changing its completion time.
/// </summary>
[Serializable, NetSerializable]
public sealed class LightAttackAimEvent : EntityEventArgs
{
    public readonly LightAttackEvent Attack;

    public LightAttackAimEvent(LightAttackEvent attack)
    {
        Attack = attack;
    }
}
