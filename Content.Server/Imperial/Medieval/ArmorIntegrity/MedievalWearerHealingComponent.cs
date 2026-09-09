using Content.Shared.Damage;

namespace Content.Server.Imperial.Medieval.ArmorIntegrity;

/// <summary>
/// Damage changes applied each second while worn as a helmet or body armor.
/// </summary>
[RegisterComponent]
public sealed partial class MedievalWearerHealingComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();
}
