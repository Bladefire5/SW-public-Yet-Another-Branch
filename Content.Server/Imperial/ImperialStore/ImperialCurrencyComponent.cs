using Robust.Shared.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.ImperialStore;

namespace Content.Server.Imperial.ImperialStore;

/// <summary>
/// Identifies a component that can be inserted into a store
/// to increase its balance.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialCurrencyComponent : Component
{
    /// <summary>
    /// The value of the currency.
    /// The string is the currency type that will be added.
    /// The FixedPoint2 is the value of each individual currency entity.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("price")]
    public Dictionary<ProtoId<ImperialCurrencyPrototype>, FixedPoint2> Price = new();
}
