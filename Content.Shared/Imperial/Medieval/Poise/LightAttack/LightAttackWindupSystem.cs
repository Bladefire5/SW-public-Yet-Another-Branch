using Content.Shared.ActionBlocker;
using Content.Shared.CombatMode;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Imperial.Medieval.ChargedAttack;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Poise.LightAttack;

/// <summary>
/// Queues light swings in shared prediction. Only the server and the attacking
/// client's prediction execute them; other clients receive the visual effect.
/// </summary>
public sealed class LightAttackWindupSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LightAttackStaminaComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LightAttackStaminaComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<LightAttackStaminaComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeAllEvent<LightAttackAimEvent>(OnAim);
    }

    public float GetWindup(EntityUid weapon, LightAttackStaminaComponent? component = null)
    {
        if (!Resolve(weapon, ref component, false))
            return 0f;

        return MathF.Max(0f, component.Windup ?? _prototypes.Index(component.Preset).Windup);
    }

    // spellward start
    public float GetInputDelay(EntityUid weapon, LightAttackStaminaComponent? component = null)
    {
        if (!Resolve(weapon, ref component, false))
            return 0f;

        return MathF.Max(0f, component.InputDelay ?? _prototypes.Index(component.Preset).InputDelay);
    }

    /// <summary>
    /// Convert each phase to whole simulation ticks. Calculate the count from the
    /// nominal rate, then use the engine's actual period: its truncated TimeSpan
    /// must not accidentally turn a nominal 27-tick wind-up into 28 ticks.
    /// </summary>
    private TimeSpan TickDuration(float seconds)
    {
        var ticks = (long) MathF.Ceiling(seconds * _timing.TickRate);
        return TimeSpan.FromTicks(ticks * _timing.TickPeriod.Ticks);
    }
    // spellward end

    public bool IsWindingUp(EntityUid weapon)
    {
        return TryComp<LightAttackStaminaComponent>(weapon, out var component) && component.PendingAttack != null;
    }

    private void OnAim(LightAttackAimEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is { } user)
            TryUpdateAim(user, args.Attack);
    }

    /// <summary>
    /// Retargets only the actor's current committed swing. Empty space and cursor
    /// positions beyond reach explicitly clear the target, rather than retaining it.
    /// The server still performs normal range and obstruction checks at impact.
    /// </summary>
    public bool TryUpdateAim(EntityUid user, LightAttackEvent aim)
    {
        if (!TryGetEntity(aim.Weapon, out var weapon) ||
            !TryComp<LightAttackStaminaComponent>(weapon, out var component) ||
            component.Attacker != user || component.PendingAttack is not { } pending ||
            pending.WindupId != aim.WindupId ||
            !_melee.TryGetWeapon(user, out var currentWeapon, out var melee) || currentWeapon != weapon)
        {
            return false;
        }

        if (!TryGetEntity(aim.Coordinates.NetEntity, out _))
            return false;

        var coordinates = GetCoordinates(aim.Coordinates);
        if (!coordinates.IsValid(EntityManager))
            return false;

        var mapCoordinates = _transform.ToMapCoordinates(coordinates);
        if (mapCoordinates.MapId != Transform(user).MapID)
            return false;

        EntityUid? target = null;
        if (aim.Target is { } netTarget && TryGetEntity(netTarget, out var resolvedTarget) &&
            !Deleted(resolvedTarget) && resolvedTarget != weapon)
        {
            target = resolvedTarget;
        }

        if (System.Numerics.Vector2.DistanceSquared(mapCoordinates.Position, _transform.GetWorldPosition(user)) > melee.Range * melee.Range)
            target = null;

        component.PendingAttack = new LightAttackEvent(GetNetEntity(target), aim.Weapon, aim.Coordinates, pending.WindupId);
        Dirty(weapon.Value, component);
        return true;
    }

    /// <summary>
    /// Called only after the normal cooldown, combat-mode and action-blocker checks.
    /// The original click is committed; releasing the mouse does not cancel it.
    /// </summary>
    public void Start(EntityUid user, EntityUid weapon, MeleeWeaponComponent melee, LightAttackEvent attack)
    {
        var component = Comp<LightAttackStaminaComponent>(weapon);
        component.PendingAttack = attack;
        component.Attacker = user;
        component.WindupStarted = _timing.CurTime;
        // spellward start
        var animationStarts = component.WindupStarted + TickDuration(GetInputDelay(weapon, component));
        component.WindupEnds = animationStarts + TickDuration(GetWindup(weapon, component));
        // spellward end

        var userTransform = Transform(user);
        var effect = PredictedSpawnAttachedTo(component.WindupEffect, userTransform.Coordinates);
        _transform.SetParent(effect, user);
        _transform.SetLocalPosition(effect, System.Numerics.Vector2.Zero);
        _transform.SetLocalRotation(effect, Angle.Zero);
        component.Effect = effect;

        // spellward start
        // Spawn and replicate the hidden effect immediately. Waiting to send it
        // until the input delay ends would waste the intended network head start.
        var visual = EnsureComp<LightAttackWindupVisualComponent>(effect);
        visual.Starts = animationStarts;
        visual.Ends = component.WindupEnds;
        Dirty(effect, visual);
        // spellward end

        // Reuse the existing press/release latch so holding the button does not queue swings.
        melee.Attacking = true;
        DirtyField(weapon, melee, nameof(MeleeWeaponComponent.Attacking));
        Dirty(weapon, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<LightAttackStaminaComponent, MeleeWeaponComponent>();
        while (query.MoveNext(out var weapon, out var component, out var melee))
        {
            if (component.PendingAttack is not { } attack || component.Attacker is not { } user || Paused(weapon))
                continue;

            if (_net.IsClient && _players.LocalEntity != user)
                continue;

            // Resolve ownership and ability again throughout the wind-up. This also
            // respects parry's attack lockout instead of finishing a swing through it.
            if (Deleted(user) || !_combat.IsInCombatMode(user) ||
                !_melee.TryGetWeapon(user, out var currentWeapon, out _) || currentWeapon != weapon ||
                !_blocker.CanAttack(user, weapon: (weapon, melee)) || melee.NextAttack > _timing.CurTime ||
                TryComp<ChargedAttackComponent>(weapon, out var charged) && charged.CurrentAttacking)
            {
                Cancel(weapon, component, melee);
                continue;
            }

            if (_timing.CurTime < component.WindupEnds)
                continue;

            // A disappearing target is a miss, not a way to cancel a committed swing.
            if (attack.Target is { } target && (!TryGetEntity(target, out var resolvedTarget) || Deleted(resolvedTarget)))
                attack = new LightAttackEvent(null, attack.Weapon, attack.Coordinates, attack.WindupId);

            var started = component.WindupStarted;
            Clear(weapon, component);
            _melee.CompleteLightAttackWindup(user, weapon, melee, attack, started);
        }
    }

    private void Cancel(EntityUid weapon, LightAttackStaminaComponent component, MeleeWeaponComponent melee)
    {
        Clear(weapon, component);
        melee.Attacking = false;
        DirtyField(weapon, melee, nameof(MeleeWeaponComponent.Attacking));
    }

    private void Clear(EntityUid weapon, LightAttackStaminaComponent component)
    {
        if (component.Effect is { } effect && !Deleted(effect))
            PredictedQueueDel(effect);

        component.Effect = null;
        component.PendingAttack = null;
        component.Attacker = null;
        component.WindupStarted = TimeSpan.Zero;
        component.WindupEnds = TimeSpan.Zero;
        Dirty(weapon, component);
    }

    private void OnShutdown(EntityUid weapon, LightAttackStaminaComponent component, ComponentShutdown args)
    {
        if (component.Effect is { } effect && !Deleted(effect))
            PredictedQueueDel(effect);
    }

    private void OnDropped(EntityUid weapon, LightAttackStaminaComponent component, DroppedEvent args)
    {
        CancelIfPending(weapon, component);
    }

    private void OnUnequipped(EntityUid weapon, LightAttackStaminaComponent component, GotUnequippedHandEvent args)
    {
        CancelIfPending(weapon, component);
    }

    private void CancelIfPending(EntityUid weapon, LightAttackStaminaComponent component)
    {
        if (component.PendingAttack != null && TryComp<MeleeWeaponComponent>(weapon, out var melee))
            Cancel(weapon, component, melee);
    }
}
