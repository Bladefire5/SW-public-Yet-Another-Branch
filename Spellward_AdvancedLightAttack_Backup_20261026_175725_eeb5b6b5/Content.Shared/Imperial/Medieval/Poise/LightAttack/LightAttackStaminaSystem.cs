using Content.Shared.Damage.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Poise.LightAttack;

/// <summary>
/// Spends stamina immediately before an accepted light melee swing.
/// Runs in shared prediction; the server independently validates the same cost.
/// </summary>
public sealed class LightAttackStaminaSystem : EntitySystem
{
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    /// <summary>
    /// Resolves the per-weapon override, then the selected preset. Weapons without
    /// this component have no cost; the component's default preset costs five.
    /// </summary>
    public float GetStaminaCost(EntityUid weapon, LightAttackStaminaComponent? component = null)
    {
        if (!Resolve(weapon, ref component, false))
            return 0f;

        var cost = component.StaminaCost ?? _prototypes.Index(component.Preset).StaminaCost;
        return MathF.Max(0f, cost);
    }

    /// <summary>
    /// Returns false when the attacker cannot afford the weapon's light attack.
    /// Weapons without the component and attackers without stamina keep their normal behavior.
    /// </summary>
    public bool TrySpendStamina(EntityUid user, EntityUid weapon)
    {
        var cost = GetStaminaCost(weapon);
        if (cost <= 0f)
            return true;

        // This is exertion, so armor must not reduce it. Do not pass the weapon as
        // "with": poise protection can cancel incoming stamina damage from weapons.
        return _stamina.TryTakeStamina(user, cost, ignoreResist: true, log: false);
    }
}
