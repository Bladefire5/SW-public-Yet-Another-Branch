using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.ToggleHair.Components;

[RegisterComponent]
public sealed partial class MedievalToggleHairComponent : Component
{
    [ViewVariables] // VVAccess.ReadOnly
    public EntityUid? Action;
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntProtoId PrototypeID = "HideHairAction";
}
public sealed partial class HideHairToggleEvent : InstantActionEvent { }
