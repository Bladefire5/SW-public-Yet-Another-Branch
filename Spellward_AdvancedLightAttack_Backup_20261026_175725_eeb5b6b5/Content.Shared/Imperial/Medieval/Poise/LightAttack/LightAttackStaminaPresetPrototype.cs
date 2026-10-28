// spellward - restore the stamina-only version, before light attack preparation.
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
}
