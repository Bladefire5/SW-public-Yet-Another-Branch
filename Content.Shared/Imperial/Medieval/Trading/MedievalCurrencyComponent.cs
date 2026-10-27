using Robust.Shared.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Store;

namespace Content.Shared.Imperial.Medieval.Trading;

[RegisterComponent]
public sealed partial class MedievalCurrencyComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("price")]
    public Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> Price = new();
}
