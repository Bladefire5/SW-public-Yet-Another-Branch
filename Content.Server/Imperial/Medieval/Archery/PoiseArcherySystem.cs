
using System;
using System.Collections.Generic;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Archery;
using Content.Shared.Imperial.Medieval.Poise;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server.Imperial.Medieval.Archery;

public sealed class PoiseArcherySystem : EntitySystem
{
    [Dependency] private readonly MedievalBowChargeSystem _bowSystem = default!;
    [Dependency] private readonly PoiseSystem _poiseSystem = default!;

    private readonly List<PendingPoiseHit> _pendingPoiseHits = new();

    private readonly record struct PendingPoiseHit(EntityUid Target, EntityUid? Shooter, EntityUid? Weapon, int Damage);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalBowChargeComponent, TakeAmmoEvent>(OnTakeAmmo, after: [typeof(SharedGunSystem)]);
        SubscribeLocalEvent<PoiseArcheryComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnTakeAmmo(EntityUid bow, MedievalBowChargeComponent charge, TakeAmmoEvent args)
    {
        if (!charge.IsCharging || !charge.AllowShot || charge.ChargingUser != args.User)
            return;

        var chargeMultiplier = _bowSystem.GetChargeMultiplier(bow);

        if (chargeMultiplier <= 0f)
            return;

        var chargeRange = charge.MaxChargeMultiplier - charge.MinChargeMultiplier;

        var progress = chargeRange <= 0f
            ? 1f
            : Math.Clamp((chargeMultiplier - charge.MinChargeMultiplier) / chargeRange, 0f, 1f);

        foreach (var (entity, _) in args.Ammo)
        {
            if (entity is not { } arrowUid || !TryComp<PoiseArcheryComponent>(arrowUid, out var arrow) || !TryComp<ProjectileComponent>(arrowUid, out var projectile))
                continue;

            var primaryDamage = arrow.FixedDamage + charge.BaseBowDamage * chargeMultiplier * arrow.DamageMultiplier;

            var bluntDamage = charge.BaseBluntImpact * chargeMultiplier * arrow.BluntMultiplier;

            var damage = new DamageSpecifier();

            damage.DamageDict[arrow.DamageType] = FixedPoint2.New(MathF.Max(0f, primaryDamage));

            if (bluntDamage > 0f)
            {
                damage.DamageDict.TryGetValue("Blunt", out var existingBlunt);
                damage.DamageDict["Blunt"] = existingBlunt + FixedPoint2.New(bluntDamage);
            }

            projectile.Damage = damage;
            var poiseMultiplier = arrow.MinPoiseMultiplier + (arrow.MaxPoiseMultiplier - arrow.MinPoiseMultiplier) * progress;
            arrow.FiredPoiseDamage = (int)MathF.Round(MathF.Max(0f, charge.BasePoiseDamage * chargeMultiplier * poiseMultiplier), MidpointRounding.AwayFromZero);

            arrow.PoiseHitApplied = false;
        }
    }

    private void OnProjectileHit(EntityUid uid, PoiseArcheryComponent arrow, ref ProjectileHitEvent args)
    {
        if (arrow.PoiseHitApplied || arrow.FiredPoiseDamage <= 0 || !HasComp<PoiseComponent>(args.Target))
            return;

        arrow.PoiseHitApplied = true;

        var weapon = TryComp<ProjectileComponent>(uid, out var projectile)
            ? projectile.Weapon
            : null;

        _pendingPoiseHits.Add(new PendingPoiseHit(args.Target, args.Shooter, weapon, arrow.FiredPoiseDamage));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var hit in _pendingPoiseHits)
        {
            if (!Exists(hit.Target) || !HasComp<PoiseComponent>(hit.Target))
                continue;

            _poiseSystem.TakePoiseDamage(hit.Target, hit.Damage, source: hit.Shooter, with: hit.Weapon);
        }

        _pendingPoiseHits.Clear();
    }
}
