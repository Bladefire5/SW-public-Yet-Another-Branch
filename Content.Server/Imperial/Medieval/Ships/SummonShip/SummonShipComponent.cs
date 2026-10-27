using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Ships.SummonShip;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class SummonShipComponent : Component
{
    [DataField(required: true)]
    public EntProtoId File = "/Maps/Imperial/Medieval/Ships/Victoria.yml";

    [DataField]
    public float Delay = 2;
}
