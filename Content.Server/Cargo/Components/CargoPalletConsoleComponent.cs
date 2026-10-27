using Robust.Shared.Prototypes;
using Content.Server.Cargo.Systems;
using Content.Shared.Stacks;

namespace Content.Server.Cargo.Components;

[RegisterComponent]
[Access(typeof(CargoSystem))]
public sealed partial class CargoPalletConsoleComponent : Component
// Imperial Space Pirates: New Horizon; Start
{
    [ViewVariables(VVAccess.ReadWrite), DataField("cashType")]
    public ProtoId<StackPrototype> CashType = "Credit";
    [ViewVariables(VVAccess.ReadWrite), DataField("spawnStack")]
    public bool SpawnStack = false;
}
// Imperial Space Pirates: New Horizon; End
