using Content.Shared.Damage;

namespace Content.Shared.Wieldable.Components;

// spellward start
[RegisterComponent, Access(typeof(SharedWieldableSystem), Other = AccessPermissions.ReadExecute)]
// spellward end
public sealed partial class IncreaseDamageOnWieldComponent : Component
{
    [DataField("damage", required: true)]
    public DamageSpecifier BonusDamage = default!;
}

