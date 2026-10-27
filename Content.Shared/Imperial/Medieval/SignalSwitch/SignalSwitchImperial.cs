using Robust.Shared.Prototypes;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
namespace Content.Shared.DeviceLinking.Components;
using Robust.Shared.Serialization;

/// <summary>
///     Simple switch that will fire ports when toggled on or off with doAfter. A button is jsut a switch that signals on the
///     same port regardless of its state.
/// </summary>
[RegisterComponent]
public sealed partial class SignalSwitchImperialComponent : Component
{
    /// <summary>
    ///     The port that gets signaled when the switch turns on.
    /// </summary>
    [DataField("onPort")]
    public ProtoId<SourcePortPrototype> OnPort = "On";

    /// <summary>
    ///     The port that gets signaled when the switch turns off.
    /// </summary>
    [DataField("offPort")]
    public ProtoId<SourcePortPrototype> OffPort = "Off";

    /// <summary>
    ///     The port that gets signaled with the switch's current status.
    ///     This is only used if OnPort is different from OffPort, not in the case of a toggle switch.
    /// </summary>
    [DataField("statusPort")]
    public ProtoId<SourcePortPrototype> StatusPort = "Status";

    [DataField("state")]
    public bool State;

    [DataField("clickSound")]
    public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/lightswitch.ogg");
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan Timing = TimeSpan.FromSeconds(5);
}
[Serializable, NetSerializable]
public sealed partial class OnDoAfterSignalSwitchEvent : SimpleDoAfterEvent { }
