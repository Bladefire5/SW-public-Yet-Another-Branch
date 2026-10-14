using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Imperial.ShockWave;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Chemistry;
using Content.Shared.Imperial.ShockWave;

namespace Content.Server.Imperial.Medieval.Chemistry;

/// <summary>
/// Connects a chemical reaction to the existing shockwave system.
/// ShockWaveSystem handles target lookup, one hit per target and chemical effects.
/// Requires the shared reaction definition and the Phase 4 shockwave updates.
/// </summary>
public sealed class MedievalBurstSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float FullReactionUnits = 5f;
    private readonly Dictionary<EntityUid, WaveActivation> _waves = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExecuteEntityEffectEvent<MedievalBurstReactionEffect>>(OnExecuteBurst);
        SubscribeLocalEvent<ShockWaveComponent, ShockWaveEntityCollideEvent>(OnWaveHit);
        SubscribeLocalEvent<ShockWaveComponent, ComponentShutdown>(OnWaveShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Snapshot because component shutdown can remove entries during deletion.
        foreach (var (uid, activation) in _waves.ToArray())
        {
            if (TerminatingOrDeleted(uid) || !TryComp<ShockWaveComponent>(uid, out var wave))
            {
                _waves.Remove(uid);
                continue;
            }

            // PreviousRadius is set by ShockWaveSystem when it processes the
            // expansion. Delete afterwards, so the outermost targets are handled.
            if (wave.PreviousRadius < activation.Radius)
                continue;

            _waves.Remove(uid);
            QueueDel(uid);
        }
    }

    private void OnExecuteBurst(ref ExecuteEntityEffectEvent<MedievalBurstReactionEffect> args)
    {
        if (args.Args is not EntityEffectReagentArgs reagentArgs ||
            reagentArgs.Quantity <= FixedPoint2.Zero || TerminatingOrDeleted(reagentArgs.TargetEntity))
            return;

        var burst = args.Effect;
        if (burst.Radius <= 0f || float.IsNaN(burst.Radius) || float.IsInfinity(burst.Radius) ||
            burst.Effects.Count == 0)
            return;

        if (burst.Falloff != MedievalBurstFalloff.None && burst.Falloff != MedievalBurstFalloff.Linear)
            return;

        if (!_transform.TryGetMapOrGridCoordinates(reagentArgs.TargetEntity, out var coordinates))
            return;

        var strength = (float) reagentArgs.Quantity / FullReactionUnits;

        // A grid/map parent keeps the wave alive if the bottle or arrow disappears.
        // Keep the existing shockwave's default speed and update cadence.
        var uid = SpawnAttachedTo(null, coordinates.Value);
        var wave = EnsureComp<ShockWaveComponent>(uid);
        wave.MaxRadius = burst.Radius;
        wave.IncludeCenter = true;
        wave.ChemicalEffectScale = strength;
        wave.Effects = burst.Effects;

        _waves.Add(uid, new WaveActivation(strength, burst.Radius, burst.Falloff));
    }

    private void OnWaveHit(EntityUid uid, ShockWaveComponent wave, ShockWaveEntityCollideEvent args)
    {
        if (!_waves.TryGetValue(uid, out var activation))
            return;

        var strength = activation.Strength;
        if (activation.Falloff == MedievalBurstFalloff.Linear)
        {
            var distance = Vector2.Distance(_transform.GetWorldPosition(uid),
                _transform.GetWorldPosition(args.Collided));
            strength *= MathF.Max(0f, 1f - distance / activation.Radius);
        }

        // The collision event runs before ShockWaveSystem applies its effects.
        // Always start from the original strength, not the previous target's scale.
        wave.ChemicalEffectScale = strength;
    }

    private void OnWaveShutdown(EntityUid uid, ShockWaveComponent component, ComponentShutdown args)
    {
        _waves.Remove(uid);
    }

    private readonly struct WaveActivation
    {
        public readonly float Strength;
        public readonly float Radius;
        public readonly MedievalBurstFalloff Falloff;

        public WaveActivation(float strength, float radius, MedievalBurstFalloff falloff)
        {
            Strength = strength;
            Radius = radius;
            Falloff = falloff;
        }
    }
}
