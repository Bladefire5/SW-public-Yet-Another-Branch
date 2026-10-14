using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Chemistry;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Medieval.Chemistry;

/// <summary>
/// Starts stock smoke/foam with an isolated, proportionally scaled payload.
/// Requires the shared MedievalChemicalCloudReactionEffect from Phase 4 Step 11.
/// </summary>
public sealed class MedievalChemicalCloudSystem : EntitySystem
{
    [Dependency] private readonly SmokeSystem _smoke = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    // Five units of each amount-1 reactant are the full payload reference.
    private const int FullReactionHundredths = 500;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExecuteEntityEffectEvent<MedievalChemicalCloudReactionEffect>>(OnExecuteCloud);
    }

    private void OnExecuteCloud(ref ExecuteEntityEffectEvent<MedievalChemicalCloudReactionEffect> args)
    {
        if (args.Args is not EntityEffectReagentArgs reagentArgs ||
            reagentArgs.Quantity <= FixedPoint2.Zero)
            return;

        var effect = args.Effect;
        if (effect.Duration <= 0f || effect.Quantity < FixedPoint2.Zero || effect.SpreadAmount < 0)
            return;

        if (!_transform.TryGetMapOrGridCoordinates(reagentArgs.TargetEntity, out var coordinates))
            return;

        // Calculate in hundredths, dividing once so fractional batches retain
        // all representable payload rather than rounding the scale to 0.01.
        var payloadHundredths = (int) ((long) effect.Quantity.Value * reagentArgs.Quantity.Value /
                                      FullReactionHundredths);
        var payload = new Solution();
        payload.AddReagent(effect.Reagent, FixedPoint2.FromHundredths(payloadHundredths));

        // A map/grid parent keeps the cloud alive when an arrow or bottle goes away.
        // Normal chemistry already consumed the reaction inputs; leave its remainder intact.
        var cloud = SpawnAttachedTo(effect.PrototypeId, coordinates.Value);
        _smoke.StartSmoke(cloud, payload, effect.Duration, effect.SpreadAmount);
        _audio.PlayPvs(effect.Sound, cloud);
    }
}
