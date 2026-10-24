using Robust.Shared.Serialization;
using Robust.Shared.Timing; //spellward

namespace Content.Shared.Imperial.Medieval.Magic;


[ImplicitDataDefinitionForInheritors, Serializable, NetSerializable]
public abstract partial class MedievalSpellData
{
    // spellward start
    [DataField]
    public GameTick InputTick;
    // spellward end

    [DataField]
    public NetEntity Performer;

    [DataField]
    public NetEntity Action;

    [DataField]
    public float CastSpeedModifier;
}
