// spellward - restore the stamina-only version, before light attack preparation.
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Poise.LightAttack;

/// <summary>
/// Configures the attacker's stamina cost for each light melee swing with this weapon.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LightAttackStaminaComponent : Component
{
    /// <summary>
    /// Preset used when this weapon does not specify its own cost.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<LightAttackStaminaPresetPrototype> Preset = "Default";

    /// <summary>
    /// Optional per-weapon override. Null uses the preset; zero disables the cost.
    /// The cost is paid on each swing, whether it hits or misses.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float? StaminaCost;
}
