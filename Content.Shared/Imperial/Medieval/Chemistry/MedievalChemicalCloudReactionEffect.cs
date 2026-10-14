using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Imperial.Medieval.Chemistry;

/// <summary>
/// Requests a stock smoke or foam cloud carrying a dedicated reagent payload.
/// The server handler creates the payload independently of vessel leftovers.
/// </summary>
[DataDefinition]
public sealed partial class MedievalChemicalCloudReactionEffect : EventEntityEffect<MedievalChemicalCloudReactionEffect>
{
    /// <summary>
    /// Chemical-capable smoke or foam entity passed to the stock smoke system.
    /// </summary>
    [DataField("prototypeId", required: true,
        customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string PrototypeId = default!;

    /// <summary>
    /// Reagent carried by the cloud: ordinary Blind or the matching poison.
    /// </summary>
    [DataField("reagent", required: true,
        customTypeSerializer: typeof(PrototypeIdSerializer<ReagentPrototype>))]
    public string Reagent = default!;

    /// <summary>
    /// Payload units for a full 5u + 5u reaction. Smaller activations scale it.
    /// </summary>
    [DataField("quantity", required: true)]
    public FixedPoint2 Quantity;

    /// <summary>
    /// Cloud lifetime passed to SmokeSystem.StartSmoke.
    /// </summary>
    [DataField("duration", required: true)]
    public float Duration;

    /// <summary>
    /// Stock spreading budget passed to SmokeSystem.StartSmoke.
    /// </summary>
    [DataField("spreadAmount", required: true)]
    public int SpreadAmount;

    [DataField("sound")]
    public SoundSpecifier? Sound;

    public override bool ShouldLog => true;
    public override LogImpact LogImpact => LogImpact.High;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype,
        IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-area-reaction", ("duration", Duration));
}
