using System.Linq;
using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
// spellward start
using Content.Shared.Damage;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.FixedPoint;
// spellward end
using Content.Shared.Imperial.ShockWave;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Trigger;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.ShockWave;


public sealed class ShockWaveSystem : SharedShockWaveSystem
{
    [Dependency] private readonly PvsOverrideSystem _pvs = default!;
    [Dependency] private readonly EntityLookupSystem _lookupSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShockWaveComponent, ComponentStartup>(OnInit);
        SubscribeLocalEvent<ShockWaveComponent, MedievalAfterSpawnEntityBySpellEvent>(OnSpellSpawned);
        SubscribeLocalEvent<ShockWaveStaminaDamageComponent, ShockWaveEntityCollideEvent>(OnStaminaHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<ShockWaveComponent>();

        while (enumerator.MoveNext(out var uid, out var component))
        {
            if (component.NextUpdate >= _timing.CurTime) continue;

            component.NextUpdate = _timing.CurTime + component.UpdateRate;

            var time = (float)(_timing.CurTime - component.SpawnTime).TotalMilliseconds;
            var radius = time * component.Speed * 0.01f;

            // spellward start
            if (component.MaxRadius is { } maxRadius)
            {
                if (component.PreviousRadius >= maxRadius)
                    continue;

                radius = MathF.Min(radius, maxRadius);
            }
            // spellward end

            if (radius <= 0) continue;

            // spellward start
            var includeCenter = component.IncludeCenter && component.PreviousRadius == 0f;
            // spellward end
            var innerRadius = MathF.Max(0f, component.PreviousRadius - component.BorderWidth);
            component.PreviousRadius = radius;

            var collidedEntities = _lookupSystem.GetEntitiesInRange(uid, radius, component.CollideFlags);

            foreach (var entity in collidedEntities)
            {
                if (component.CollidedEntities.Contains(entity)) continue;
                var position = _transformSystem.GetWorldPosition(entity);
                var wavePosition = _transformSystem.GetWorldPosition(uid);

                // spellward start
                // The lookup can return a body intersecting the search circle whose
                // origin lies outside it. Keep capped waves within their actual radius.
                if (component.MaxRadius != null && EntityOutsideRange(wavePosition, position, radius))
                    continue;

                if (!EntityOutsideRange(wavePosition, position, innerRadius) &&
                    !(includeCenter && position == wavePosition))
                    continue;
                // spellward end

                component.CollidedEntities.Add(entity);

                var triggerEv = new TriggerEvent(entity);

                RaiseLocalEvent(uid, new ShockWaveEntityCollideEvent(uid, entity));
                RaiseLocalEvent(uid, ref triggerEv, true);

                // spellward start
                if (component.ChemicalEffectScale is { } strength)
                {
                    ApplyChemicalEffects(entity, component, strength);
                    continue;
                }
                // spellward end

                foreach (var effect in component.Effects)
                {
                    var args = new EntityEffectBaseArgs(entity, EntityManager);
                    var canApplyEffect = effect.Conditions?.Aggregate(true, (acc, condition) => condition.Condition(args) && acc) ?? true;

                    if (!canApplyEffect) continue;

                    effect.Effect(args);
                }
            }
        }
    }

    private void OnInit(EntityUid uid, ShockWaveComponent component, ComponentStartup args)
    {
        component.SpawnTime = _timing.CurTime;
        component.PreviousRadius = 0f;

        _pvs.AddGlobalOverride(uid);
    }

    private void OnSpellSpawned(EntityUid uid, ShockWaveComponent component, MedievalAfterSpawnEntityBySpellEvent args)
    {
        component.User = args.Performer;
    }

    private void OnStaminaHit(EntityUid uid, ShockWaveStaminaDamageComponent component, ShockWaveEntityCollideEvent args)
    {
        if (!TryComp<StaminaComponent>(args.Collided, out var stamina))
            return;

        var user = Comp<ShockWaveComponent>(uid).User;

        _stamina.TakeStaminaDamage(args.Collided,
            component.Stamina,
            stamina,
            source: user,
            with: uid,
            sound: component.Sound);
    }

    #region Helpers

    // spellward start
    /// <summary>
    /// Applies the configured effects through normal chemistry handling, using
    /// one reference unit and the activation's strength. No solution is consumed.
    /// </summary>
    private void ApplyChemicalEffects(EntityUid target, ShockWaveComponent component, float strength)
    {
        if (strength <= 0f)
            return;

        var args = new EntityEffectReagentArgs(target, EntityManager, null, null,
            FixedPoint2.New(1), null, null, FixedPoint2.New(strength));

        foreach (var effect in component.Effects)
        {
            if (TerminatingOrDeleted(target))
                break;

            if (!effect.ShouldApply(args))
                continue;

            if (effect is HealthChange healthChange)
            {
                // Scale damage before rounding the factor to hundredths. For
                // example, 25 damage at 0.01u / 5u still produces 0.05 damage.
                var damage = new DamageSpecifier(healthChange.Damage);
                foreach (var (type, amount) in damage.DamageDict)
                    damage.DamageDict[type] = amount * strength;

                var scaledHealth = new HealthChange
                {
                    Damage = damage,
                    IgnoreResistances = healthChange.IgnoreResistances,
                };
                scaledHealth.Effect(args with { Scale = FixedPoint2.New(1) });
                continue;
            }

            // Status durations and refresh behavior remain with their existing effects.
            effect.Effect(args);
        }
    }
    // spellward end

    private bool EntityOutsideRange(Vector2 v1, Vector2 v2, float range)
    {
        var dx = v1.X - v2.X;
        var dy = v1.Y - v2.Y;

        return Math.Abs(dx * dx + dy * dy) > range * range;
    }

    #endregion
}
