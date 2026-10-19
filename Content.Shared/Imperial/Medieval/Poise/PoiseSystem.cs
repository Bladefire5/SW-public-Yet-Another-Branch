using Content.Shared.CombatMode;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Damage.Events;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.ChargedAttack;
// spellward start
using Content.Shared.Explosion;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
// spellward end

namespace Content.Shared.Imperial.Medieval.Poise;

public sealed class PoiseSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    // spellward start
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    private static readonly EntProtoId NpcStagger = "MedievalStatusEffectPoiseStagger";
    // spellward end


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PoiseComponent, BeforeStaminaDamageEvent>(OnBeforeStaminaDamage);
        SubscribeLocalEvent<PoiseComponent, MeleeAttackEvent>(OnMeleeAttack);
        SubscribeLocalEvent<PoiseComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<PoiseComponent, AttackedEvent>(OnAttacked);
        // spellward start
        SubscribeLocalEvent<PoiseComponent, GetExplosionResistanceEvent>(OnExplosionResistance);
        // spellward end
    }

    private void OnAttacked(EntityUid uid, PoiseComponent component, AttackedEvent args)
    {
        if (!TryComp<PoiseDamageComponent>(args.Used, out var poise))
            return;
        if (poise.PoiseDamage <= 0)
            return;

        var damage = poise.PoiseDamage;

        if (TryComp<ChargedAttackComponent>(args.Used, out var charged) && charged.CurrentAttacking)
        {
            var multiplier = 1f + (poise.HeavyPoiseMultiplier - 1f) * charged.PoiseChargeRatio;
            damage = (int)MathF.Round(poise.PoiseDamage * multiplier, MidpointRounding.AwayFromZero);
        }

        TakePoiseDamage(uid, damage, source: args.User, with: args.Used == args.User ? null : args.Used);
    }

    public static int GetEffectivePoiseThreshold(PoiseComponent component)
    {
        // spellward start - NPC capacity is set per prototype, independently of player armor bonuses.
        if (component.Npc)
            return component.PoiseThreshold;
        // spellward end
        return component.PoiseThreshold + component.ArmorPoiseIncrease;
    }

    // spellward start
    private bool CanUsePoise(EntityUid uid, PoiseComponent component)
    {
        // NPC poise is authoritative on the server and works outside combat mode.
        if (component.Npc)
            return _net.IsServer && component.PoiseThreshold > 0;

        return TryComp<CombatModeComponent>(uid, out var combatMode) && combatMode.IsInCombatMode;
    }
    // spellward end

    public void TakePoiseDamage(
    EntityUid uid,
    int amount,
    PoiseComponent? component = null,
    EntityUid? source = null,
    EntityUid? with = null,
    bool log = true)
    {
        if (!Resolve(uid, ref component))
            return;

        // spellward start
        if (!CanUsePoise(uid, component))
            return;
        // spellward end

        if (amount <= 0)
            return;

        if (component.PoiseBroken)
            return;

        var oldDamage = component.PoiseDamage;

        component.PoiseDamage = Math.Clamp(component.PoiseDamage + amount, 0, GetEffectivePoiseThreshold(component));

        var actualDamage = component.PoiseDamage - oldDamage;

        EnsureComp<ActivePoiseComponent>(uid);

        // spellward start - every NPC poise hit restarts the partial-recovery delay.
        if (component.Npc)
            DelayPoiseRecovery(uid, component.PoiseRecoveryDelay, component);
        // spellward end

        if (log && actualDamage > 0)
        {
            if (source != null)
            {
                _adminLogger.Add(
                LogType.Poise,
                $"{ToPrettyString(source.Value):user} caused {actualDamage} poise damage to {ToPrettyString(uid):target}" +
                $"{(with != null ? $" using {ToPrettyString(with.Value):using}" : "")}" + $" ({component.PoiseDamage}/{GetEffectivePoiseThreshold(component)})");
            }
            else
            {
                _adminLogger.Add(LogType.Poise,
                $"{ToPrettyString(uid):target} took {actualDamage} poise damage");
            }
        }

        if (component.PoiseDamage >= GetEffectivePoiseThreshold(component))
        {

            BreakPoise(uid, component);
            return;
        }

        Dirty(uid, component);
    }

    private void OnMeleeAttack(EntityUid uid, PoiseComponent component, ref MeleeAttackEvent args)
    {
        // spellward start - NPC recovery depends on incoming hits, not their own attacks.
        if (component.Npc)
            return;
        // spellward end
        if (component.PoiseBroken)
            return;

        if (component.PoiseDamage <= 0)
            return;

        if (!TryComp<PoiseDamageComponent>(args.Weapon, out var weaponPoise))
            return;

        DelayPoiseRecovery(uid, weaponPoise.PoiseRecoveryDelayAfterAttack, component);
    }

    public void DelayPoiseRecovery(EntityUid uid, TimeSpan delay, PoiseComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        // spellward start
        if (component.Npc && !_net.IsServer)
            return;
        // spellward end

        if (component.PoiseBroken)
            return;

        var recoveryStarts = _timing.CurTime + delay;

        if (component.PoiseRecoveryStarts < recoveryStarts)
        {
            component.PoiseRecoveryStarts = recoveryStarts;
            component.NextPoiseRecoveryTick = recoveryStarts;
            Dirty(uid, component);
        }
    }

    public void BreakPoise(EntityUid uid, PoiseComponent component, bool protectBreakingHit = false)
    {
        // spellward start
        if (component.Npc && (!_net.IsServer || component.PoiseThreshold <= 0))
            return;
        // spellward end
        if (component.PoiseBroken)
            return;

        component.ProtectBreakingHit = protectBreakingHit;
        component.PoiseBroken = true;
        component.PoiseRecoveryStarts = _timing.CurTime + component.PoiseRecoveryDelay;
        component.NextPoiseRecoveryTick = component.PoiseRecoveryStarts;
        component.PoiseBreakEnds = _timing.CurTime + component.PoiseBreakDuration;

        // spellward start - stun without knockdown; the dedicated status permits StunImmune NPCs.
        if (component.Npc)
            _status.TrySetStatusEffectDuration(uid, NpcStagger, component.PoiseBreakDuration);
        // spellward end

        EnsureComp<ActivePoiseComponent>(uid);

        _adminLogger.Add(
        LogType.Poise,
        LogImpact.Medium,
        $"{ToPrettyString(uid):target} had their poise broken");

        Dirty(uid, component);
    }

    private void OnDamageModify(EntityUid uid, PoiseComponent component, ref DamageModifyEvent args)
    {
        // spellward start
        if (!CanUsePoise(uid, component))
            return;
        // spellward end

        var protectBreakingHit = component.ProtectBreakingHit;
        component.ProtectBreakingHit = false;

        if (component.PoiseBroken && !protectBreakingHit)
            return;

        float resistance;

        // spellward start - NPC health protection is flat and does not use armor multipliers.
        if (component.Npc)
        {
            resistance = component.HealthDamageReduction;
        }
        else if (component.HasPoiseArmor)
        {
            var cap = component.HealthDamageReductionCap;

            resistance = Math.Clamp(component.HealthDamageReduction * component.PoiseMultiplier, 0f, cap);
        }
        else
        {
            resistance = component.UnarmoredHealthDamageReduction;
        }
        // spellward end

        if (resistance <= 0f)
            return;

        var multiplier = 1f - resistance;

        var modifiedDamage = new DamageSpecifier(args.Damage);

        foreach (var (type, value) in args.Damage.DamageDict)
        {
            if (value <= FixedPoint2.Zero)
                continue;

            modifiedDamage.DamageDict[type] = FixedPoint2.New(value.Float() * multiplier);
        }

        args.Damage = modifiedDamage;
    }

    private void OnBeforeStaminaDamage(EntityUid uid, PoiseComponent component, ref BeforeStaminaDamageEvent args)
    {

        // spellward start
        if (!CanUsePoise(uid, component))
            return;
        // spellward end

        if (component.PoiseBroken)
            return;

        if (args.With is not { } weapon)
            return;

        if (!HasComp<PoiseDamageComponent>(weapon))
            return;

        args.Cancelled = true;
    }

    // spellward start - explosion damage bypasses DamageModifyEvent, so protect it separately.
    private void OnExplosionResistance(EntityUid uid, PoiseComponent component, ref GetExplosionResistanceEvent args)
    {
        if (!component.Npc || !CanUsePoise(uid, component) || component.PoiseBroken)
            return;

        args.DamageCoefficient *= 1f - component.ExplosionDamageReduction;
    }
    // spellward end

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var poiseQuery = GetEntityQuery<PoiseComponent>();
        var query = EntityQueryEnumerator<ActivePoiseComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            if (!poiseQuery.TryGetComponent(uid, out var component))
            {
                RemComp<ActivePoiseComponent>(uid);
                continue;
            }
            if (component.PoiseBroken)
            {
                component.ProtectBreakingHit = false;

                if (_timing.CurTime >= component.PoiseBreakEnds)
                    ReturnPoise(uid, component);

                continue;
            }

            if (component.PoiseDamage <= 0)
            {
                RemComp<ActivePoiseComponent>(uid);
                continue;
            }
            if (_timing.CurTime < component.PoiseRecoveryStarts)
                continue;

            if (_timing.CurTime < component.NextPoiseRecoveryTick)
                continue;
            component.PoiseDamage = Math.Max(0, component.PoiseDamage - component.PoiseRecoveryPerTick);

            component.NextPoiseRecoveryTick = _timing.CurTime + component.PoiseRecoveryInterval;

            Dirty(uid, component);
        }
    }

    private void ReturnPoise(EntityUid uid, PoiseComponent component)
    {
        component.PoiseBroken = false;

        // spellward start - eligible NPCs recover fully as soon as their stagger ends.
        if (component.Npc)
        {
            component.PoiseDamage = 0;
            _status.TryRemoveStatusEffect(uid, NpcStagger);
        }
        else
        {
            component.PoiseDamage = (int)
                Math.Round(GetEffectivePoiseThreshold(component) * (1f - component.PoiseBreakRecoveryFraction));
        }
        // spellward end

        component.PoiseRecoveryStarts = _timing.CurTime + component.PoiseRecoveryDelay;

        component.NextPoiseRecoveryTick = component.PoiseRecoveryStarts;

        Dirty(uid, component);
    }

    public int GetPoiseDamage(EntityUid uid, PoiseComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return 0;

        return component.PoiseDamage;
    }

}
