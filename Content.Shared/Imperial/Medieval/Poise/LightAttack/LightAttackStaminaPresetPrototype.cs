using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Poise.LightAttack;

/// <summary>
/// Reusable stamina cost for a weapon type, configured in YAML.
/// </summary>
[Prototype("lightAttackStaminaPreset")]
public sealed partial class LightAttackStaminaPresetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Stamina spent per light swing. Five is the default for new presets.
    /// </summary>
    [DataField]
    public float StaminaCost = 5f;

    /// <summary>
    /// Visible preparation in seconds, after input delay and before swinging.
    /// </summary>
    [DataField]
    public float Windup = 0.45f;

    // spellward start
    /// <summary>
    /// Hidden input delay in seconds. At 60 Hz the default occupies two simulation ticks.
    /// </summary>
    [DataField]
    public float InputDelay = 0.033f;
    // spellward end
}
