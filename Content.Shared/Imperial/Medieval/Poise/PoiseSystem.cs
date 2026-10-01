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

namespace Content.Shared.Imperial.Medieval.Poise;


public sealed class PoiseSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PoiseComponent, BeforeStaminaDamageEvent>(OnBeforeStaminaDamage);
        SubscribeLocalEvent<PoiseComponent, MeleeAttackEvent>(OnMeleeAttack);
        SubscribeLocalEvent<PoiseComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<PoiseComponent, AttackedEvent>(OnAttacked);
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

        if (!TryComp<CombatModeComponent>(uid, out var combatMode))
            return;

        if (!combatMode.IsInCombatMode)
            return;

        if (amount <= 0)
            return;

        if (component.PoiseBroken)
            return;

        var oldDamage = component.PoiseDamage;

        component.PoiseDamage = Math.Clamp(component.PoiseDamage + amount, 0, component.PoiseThreshold);

        var actualDamage = component.PoiseDamage - oldDamage;

        EnsureComp<ActivePoiseComponent>(uid);

        if (log && actualDamage > 0)
        {
            if (source != null)
            {
                _adminLogger.Add(
                LogType.Poise,
                $"{ToPrettyString(source.Value):user} caused {actualDamage} poise damage to {ToPrettyString(uid):target}" +
                $"{(with != null ? $" using {ToPrettyString(with.Value):using}" : "")}" + $" ({component.PoiseDamage}/{component.PoiseThreshold})");
            }
            else
            {
                _adminLogger.Add(LogType.Poise,
                $"{ToPrettyString(uid):target} took {actualDamage} poise damage");
            }
        }

        if (component.PoiseDamage >= component.PoiseThreshold)
        {
            BreakPoise(uid, component);
            return;
        }

        component.PoiseRecoveryStarts = _timing.CurTime + component.PoiseRecoveryDelay;

        component.NextPoiseRecoveryTick = component.PoiseRecoveryStarts;

        Dirty(uid, component);
    }

    private void OnMeleeAttack(EntityUid uid, PoiseComponent component, ref MeleeAttackEvent args)
    {
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

    private void BreakPoise(EntityUid uid, PoiseComponent component)
    {
        component.PoiseBroken = true;

        component.PoiseBreakEnds = _timing.CurTime + component.PoiseBreakDuration;

        _adminLogger.Add(
        LogType.Poise,
        LogImpact.Medium,
        $"{ToPrettyString(uid):target} had their poise broken");

        Dirty(uid, component);
    }

    private void OnDamageModify(EntityUid uid, PoiseComponent component, ref DamageModifyEvent args)
    {
        if (!TryComp<CombatModeComponent>(uid, out var combatMode) || !combatMode.IsInCombatMode)
            return;

        if (component.PoiseBroken)
            return;

        if (component.HealthDamageReduction <= 0f)
            return;

        var resistance = Math.Clamp(component.HealthDamageReduction, 0f, 1f);
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

        if (!TryComp<CombatModeComponent>(uid, out var combatMode) || !combatMode.IsInCombatMode)
            return;

        if (component.PoiseBroken)
            return;

        if (args.With is not { } weapon)
            return;

        if (!HasComp<PoiseDamageComponent>(weapon))
            return;

        args.Cancelled = true;
    }

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

        component.PoiseDamage = (int)
        Math.Round(component.PoiseThreshold * (1f - component.PoiseBreakRecoveryFraction));

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
