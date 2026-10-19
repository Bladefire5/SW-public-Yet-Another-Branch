using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Poise;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PoiseDamageComponent : Component
{
    [DataField, AutoNetworkedField]
    public int PoiseDamage = 10;

    // spellward start - per-shot override for NPC guns, independent of their melee attack.
    [DataField, AutoNetworkedField]
    public int? RangedPoiseDamage;
    // spellward end

    [DataField, AutoNetworkedField]
    public float HeavyPoiseMultiplier = 2f;

    [DataField, AutoNetworkedField]
    public TimeSpan PoiseRecoveryDelayAfterAttack = TimeSpan.FromSeconds(5);
}
