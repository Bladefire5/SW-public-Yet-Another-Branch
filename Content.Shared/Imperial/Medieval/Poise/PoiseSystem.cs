using Content.Shared.CombatMode;
// spellward start - worn equipment can resist incoming poise damage.
using Content.Shared.Inventory;
// spellward end
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Damage.Events;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.ChargedAttack;
// spellward start - normal mounted lance two-handed poise
using Content.Shared.Imperial.Medieval.Lances;
using Content.Shared.Wieldable.Components;
// spellward end
// spellward start
using Content.Shared.Explosion;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
// spellward end

namespace Content.Shared.Imperial.Medieval.Poise;

public sealed class PoiseSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    // spellward start
    [Dependency] private readonly InventorySystem _inventory = default!;
    // spellward end
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
        // spellward start
        var damage = args.PoiseDamageOverride ?? GetMeleePoiseDamage(args.Used);
        if (damage > 0)
            TakePoiseDamage(uid, damage, source: args.User, with: args.Used == args.User ? null : args.Used);
    }

    /// <summary>Capture charge/grip-dependent poise at contact, before hit confirmation.</summary>
    public int GetMeleePoiseDamage(EntityUid weapon)
    {
        if (!TryComp<PoiseDamageComponent>(weapon, out var poise))
            return 0;
        if (poise.PoiseDamage <= 0)
            return 0;

        var damage = poise.PoiseDamage;
        // spellward start - the normal lance gains eight poise when wielded in two hands.
        if (TryComp<LanceMeleeComponent>(weapon, out var lance) &&
            TryComp<WieldableComponent>(weapon, out var wieldable) && wieldable.Wielded)
            damage += lance.TwoHandedPoiseBonus;
        // spellward end

        if (TryComp<ChargedAttackComponent>(weapon, out var charged) && charged.CurrentAttacking)
        {
            var multiplier = 1f + (poise.HeavyPoiseMultiplier - 1f) * charged.PoiseChargeRatio;
            // spellward start - preserve the lance's current grip bonus when charging.
            damage = (int)MathF.Round(damage * multiplier, MidpointRounding.AwayFromZero);
            // spellward end
        }

        return damage;
        // spellward end
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

        // spellward start - reduce the hit before accumulating damage or checking for a break.
        amount = (int)MathF.Round(amount * GetPoiseDamageCoefficient(uid), MidpointRounding.AwayFromZero);
        if (amount <= 0)
            return;
        // spellward end

        // spellward start - ordinary hits use resistance; combat-exit damage enters below it.
        ApplyPoiseDamage(uid, amount, component, source, with, log);
    }

    private void ApplyPoiseDamage(
        EntityUid uid,
        int amount,
        PoiseComponent component,
        EntityUid? source = null,
        EntityUid? with = null,
        bool log = true)
    {
        // spellward end
        var oldDamage = component.PoiseDamage;

        component.PoiseDamage = Math.Clamp(component.PoiseDamage + amount, 0, GetEffectivePoiseThreshold(component));

        var actualDamage = component.PoiseDamage - oldDamage;

        EnsureComp<ActivePoiseComponent>(uid);

        // spellward start - every poise hit restarts the target's recovery delay.
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

    // spellward start - called only for a real transition out of combat mode.
    public void OnCombatModeExited(EntityUid uid)
    {
        if (!TryComp<PoiseComponent>(uid, out var component) || component.Npc)
            return;

        // This deadline bypasses every armor timer bonus and survives re-entering combat.
        component.PoiseRecoveryBlockedUntil = _timing.CurTime + TimeSpan.FromSeconds(30);

        // Already-broken poise is capped; still restart the full recovery lock above.
        if (!component.PoiseBroken)
            ApplyPoiseDamage(uid, 30, component);

        // Keep the existing HUD countdown aligned with the enforced broken state.
        if (component.PoiseBroken && component.PoiseBreakEnds < component.PoiseRecoveryBlockedUntil)
            component.PoiseBreakEnds = component.PoiseRecoveryBlockedUntil;

        EnsureComp<ActivePoiseComponent>(uid);
        Dirty(uid, component);
    }

    private TimeSpan GetRecoveryTime(EntityUid uid, PoiseComponent component, TimeSpan duration)
    {
        if (component.Npc || !TryComp<InventoryComponent>(uid, out var inventory))
            return duration;

        // Only a worn torso qualifies, using its own capacity rather than the whole set.
        var slots = _inventory.GetSlotEnumerator((uid, inventory), SlotFlags.OUTERCLOTHING);
        while (slots.NextItem(out var item))
        {
            if (TryComp<PoiseArmorComponent>(item, out var armor) &&
                armor.ProvidesArmorBonuses && armor.GetPoiseIncrease() < 9f)
                return TimeSpan.FromTicks(duration.Ticks / 2);
        }

        return duration;
    }
    // spellward end

    // spellward start - query current equipment so equipping/removing the item works during prediction.
    private float GetPoiseDamageCoefficient(EntityUid uid)
    {
        var coefficient = 1f;

        if (TryComp<PoiseArmorComponent>(uid, out var innate))
            coefficient *= Math.Clamp(innate.DamageCoefficient, 0f, 1f);

        if (!TryComp<InventoryComponent>(uid, out var inventory))
            return coefficient;

        var slots = _inventory.GetSlotEnumerator((uid, inventory), SlotFlags.WITHOUT_POCKET);
        while (slots.NextItem(out var item))
        {
            if (TryComp<PoiseArmorComponent>(item, out var resistance))
                coefficient *= Math.Clamp(resistance.DamageCoefficient, 0f, 1f);
        }

        return coefficient;
    }
    // spellward end

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

        // spellward start - ordinary delay is armor-sensitive; the exit lock is separate.
        var recoveryStarts = _timing.CurTime + GetRecoveryTime(uid, component, delay);
        // spellward end

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
        // spellward start - halve break and recovery timers for qualifying torsos.
        component.PoiseRecoveryStarts = _timing.CurTime + GetRecoveryTime(uid, component, component.PoiseRecoveryDelay);
        component.NextPoiseRecoveryTick = component.PoiseRecoveryStarts;
        component.PoiseBreakEnds = _timing.CurTime + GetRecoveryTime(uid, component, component.PoiseBreakDuration);
        // A later hit can break poise during an existing combat-exit lock.
        if (component.PoiseBreakEnds < component.PoiseRecoveryBlockedUntil)
            component.PoiseBreakEnds = component.PoiseRecoveryBlockedUntil;
        // spellward end

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
        // spellward start - broken vulnerability persists until recovery, including outside combat.
        // NPC state remains authoritative on the server.
        if (component.Npc && !_net.IsServer)
            return;

        var protectBreakingHit = component.ProtectBreakingHit;
        component.ProtectBreakingHit = false;

        float multiplier;

        if (component.PoiseBroken && !protectBreakingHit)
        {
            multiplier = component.BrokenHealthDamageMultiplier;
            if (multiplier == 1f)
                return;
        }
        else
        {
            if (!CanUsePoise(uid, component))
                return;

            float resistance;

            // NPC health protection is flat; player protection follows the existing armor rules.
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

            if (resistance <= 0f)
                return;

            multiplier = 1f - resistance;
        }

        var modifiedDamage = new DamageSpecifier(args.Damage);

        foreach (var (type, value) in args.Damage.DamageDict)
        {
            if (value <= FixedPoint2.Zero)
                continue;

            modifiedDamage.DamageDict[type] = FixedPoint2.New(value.Float() * multiplier);
        }

        args.Damage = modifiedDamage;
        // spellward end
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

                // spellward start - returning from a break also restores poise, so honor the lock.
                if (_timing.CurTime >= component.PoiseBreakEnds &&
                    _timing.CurTime >= component.PoiseRecoveryBlockedUntil)
                    ReturnPoise(uid, component);
                // spellward end

                continue;
            }

            if (component.PoiseDamage <= 0)
            {
                RemComp<ActivePoiseComponent>(uid);
                continue;
            }
            // spellward start - never shorten this deadline with armor or combat toggles.
            if (_timing.CurTime < component.PoiseRecoveryBlockedUntil)
                continue;
            // spellward end
            if (_timing.CurTime < component.PoiseRecoveryStarts)
                continue;

            if (_timing.CurTime < component.NextPoiseRecoveryTick)
                continue;
            component.PoiseDamage = Math.Max(0, component.PoiseDamage - component.PoiseRecoveryPerTick);

            // spellward start - low-poise torsos recover twice as often.
            component.NextPoiseRecoveryTick = _timing.CurTime + GetRecoveryTime(uid, component, component.PoiseRecoveryInterval);
            // spellward end

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

        // spellward start
        component.PoiseRecoveryStarts = _timing.CurTime + GetRecoveryTime(uid, component, component.PoiseRecoveryDelay);
        // spellward end

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

