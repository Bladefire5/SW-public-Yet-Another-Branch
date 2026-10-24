using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared.Imperial.Medieval.Poise.LightAttack;

/// <summary>
/// Configures the attacker's stamina cost for each light melee swing with this weapon.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
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

    /// <summary>
    /// Optional visible wind-up in seconds. Null uses the preset; zero disables
    /// the wind-up animation. Set inputDelay to zero too for instant light attacks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float? Windup;

    // spellward start
    /// <summary>
    /// Hidden preparation before the visible wind-up, in seconds. Null uses the preset.
    /// The attack is committed and sent immediately; zero disables this input delay.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float? InputDelay;
    // spellward end

    [DataField, AutoNetworkedField]
    public EntProtoId WindupEffect = "MedievalLightAttackWindup";

    // Network these fields so rollback restores a pending swing instead of starting it twice.
    [ViewVariables, AutoNetworkedField]
    public LightAttackEvent? PendingAttack;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Attacker;

    // spellward start
    /// <summary>
    /// Time the click was accepted, before input delay. Also anchors the attack cooldown.
    /// </summary>
    // spellward end
    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan WindupStarted;

    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan WindupEnds;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Effect;
}
