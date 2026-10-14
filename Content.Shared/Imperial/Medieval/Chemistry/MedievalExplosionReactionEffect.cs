using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.Explosion;
using Content.Shared.Explosion.EntitySystems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Imperial.Medieval.Chemistry;

/// <summary>
/// Queues a stock explosion proportional to the capped medieval reaction amount.
/// The chemistry engine handles consumption and the interval between reactions.
/// </summary>
[DataDefinition]
public sealed partial class MedievalExplosionReactionEffect : EntityEffect
{
    // Five units of each amount-1 reactant are the full-strength reference.
    private const float FullReactionUnits = 5f;

    /// <summary>
    /// Dedicated alchemical explosion prototype defining damage and visuals.
    /// </summary>
    [DataField("explosionType", required: true,
        customTypeSerializer: typeof(PrototypeIdSerializer<ExplosionPrototype>))]
    public string ExplosionType = default!;

    /// <summary>
    /// Total blast intensity for a full 5u + 5u activation.
    /// </summary>
    [DataField("totalIntensity", required: true)]
    public float TotalIntensity;

    /// <summary>
    /// Stock intensity falloff away from the epicenter.
    /// </summary>
    [DataField("intensitySlope", required: true)]
    public float IntensitySlope;

    /// <summary>
    /// Stock upper limit on intensity at an individual tile.
    /// </summary>
    [DataField("maxIntensity", required: true)]
    public float MaxIntensity;

    public override bool ShouldLog => true;
    public override LogImpact LogImpact => LogImpact.High;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype,
        IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-explosion-reaction-effect", ("chance", Probability));

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagentArgs ||
            !IoCManager.Resolve<INetManager>().IsServer)
            return;

        var strength = (float) reagentArgs.Quantity / FullReactionUnits;
        args.EntityManager.System<SharedExplosionSystem>().QueueExplosion(
            args.TargetEntity,
            ExplosionType,
            TotalIntensity * strength,
            IntensitySlope,
            MaxIntensity,
            tileBreakScale: 0f,
            maxTileBreak: 0,
            canCreateVacuum: false);
    }
}
