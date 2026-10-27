using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.Explosion;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Radiation.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

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
    /// Existing explosion preset defining damage and visuals, or the shared Caustic preset.
    /// </summary>
    [DataField("explosionType", required: true)]
    public ProtoId<ExplosionPrototype> ExplosionType = default!;

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

    /// <summary>
    /// Optional lingering radiation source for this reaction. Its prototype defines
    /// intensity, falloff, light and lifetime for a full 5u + 5u activation.
    /// </summary>
    [DataField("radiationFieldPrototype")]
    public EntProtoId? RadiationFieldPrototype;

    public override bool ShouldLog => true;
    public override LogImpact LogImpact => LogImpact.High;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype,
        IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-explosion-reaction-effect", ("chance", Probability));

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagentArgs ||
            reagentArgs.Quantity <= FixedPoint2.Zero ||
            !IoCManager.Resolve<INetManager>().IsServer)
            return;

        var strength = (float) reagentArgs.Quantity / FullReactionUnits;

        if (RadiationFieldPrototype is { } fieldPrototype)
        {
            var transform = args.EntityManager.System<SharedTransformSystem>();
            if (transform.TryGetMapOrGridCoordinates(args.TargetEntity, out var coordinates))
            {
                // A ground parent keeps the field alive after the vessel is deleted.
                var field = args.EntityManager.SpawnAttachedTo(fieldPrototype, coordinates.Value);
                if (args.EntityManager.TryGetComponent<RadiationSourceComponent>(field, out var source))
                {
                    // Keep one field per activation, including fractional batches.
                    source.Intensity *= strength;
                    source.Slope *= strength;
                }
            }
        }

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
