namespace Content.Shared.Imperial.Medieval.CombatArts.Physical;


[RegisterComponent]
public sealed partial class MedievalPhysicalComponent : Component
{
    [DataField]
    public float StaminaCost = 0;

    [DataField]
    public bool IgnoreStaminaResistance = true;

}