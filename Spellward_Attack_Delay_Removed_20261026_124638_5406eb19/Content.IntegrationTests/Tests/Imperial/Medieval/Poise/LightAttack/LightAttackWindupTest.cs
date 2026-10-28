using System.Numerics;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Imperial.Medieval.Poise.LightAttack;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.Poise.LightAttack;

[TestFixture]
public sealed class LightAttackWindupTest
{
    [Test]
    public async Task InputDelayAt60HzSendsTheEffectBeforeTheAnimationAndAllowsRetargeting()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var map = await pair.CreateTestMap();
        var entMan = pair.Server.EntMan;
        EntityUid actor = default;
        EntityUid oldTarget = default;
        EntityUid newTarget = default;

        await pair.Server.WaitAssertion(() =>
        {
            pair.Server.CfgMan.SetCVar(CVars.NetTickrate, 60);
            var timing = pair.Server.ResolveDependency<IGameTiming>();
            actor = entMan.SpawnEntity("LightAttackWindupTestActor", map.GridCoords);
            oldTarget = entMan.SpawnEntity("LightAttackWindupTestTarget", map.GridCoords);
            newTarget = entMan.SpawnEntity("LightAttackWindupTestTarget", map.GridCoords.Offset(new Vector2(0.5f, 0)));
            var melee = entMan.GetComponent<MeleeWeaponComponent>(actor);
            Assert.That(entMan.System<SharedMeleeWeaponSystem>().AttemptLightAttack(actor, actor, melee, oldTarget), Is.True);

            var pending = entMan.GetComponent<LightAttackStaminaComponent>(actor);
            // The entity and its schedule exist immediately, while its sprite is hidden.
            Assert.That(pending.Effect, Is.Not.Null);
            var visual = entMan.GetComponent<LightAttackWindupVisualComponent>(pending.Effect!.Value);
            Assert.That(visual.Starts - pending.WindupStarted, Is.EqualTo(TimeSpan.FromTicks(timing.TickPeriod.Ticks * 2)));
            Assert.That(visual.Ends - visual.Starts, Is.EqualTo(TimeSpan.FromTicks(timing.TickPeriod.Ticks * 27)));
            Assert.That(visual.TryGetProgress(visual.Starts - TimeSpan.FromTicks(1), out _), Is.False);
            Assert.That(visual.TryGetProgress(visual.Starts, out var progress), Is.True);
            Assert.That(progress, Is.Zero);
            Assert.That(visual.TryGetProgress(visual.Ends, out _), Is.False);

            var ends = pending.WindupEnds;
            var aim = new LightAttackEvent(entMan.GetNetEntity(newTarget), entMan.GetNetEntity(actor),
                entMan.GetNetCoordinates(map.GridCoords.Offset(new Vector2(0.5f, 0))), pending.PendingAttack!.WindupId);
            Assert.That(entMan.System<LightAttackWindupSystem>().TryUpdateAim(actor, aim), Is.True);
            Assert.That(pending.WindupEnds, Is.EqualTo(ends));
            Assert.That(entMan.GetComponent<StaminaComponent>(actor).StaminaDamage, Is.Zero);
        });

        // Two hidden ticks plus only 26 visible ticks is still before impact.
        await pair.RunTicksSync(28);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<LightAttackStaminaComponent>(actor).PendingAttack, Is.Not.Null);
            Assert.That(entMan.GetComponent<StaminaComponent>(actor).StaminaDamage, Is.Zero);
            Assert.That(entMan.GetComponent<DamageableComponent>(newTarget).TotalDamage.Float(), Is.Zero);
        });

        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<LightAttackStaminaComponent>(actor).PendingAttack, Is.Null);
            Assert.That(entMan.GetComponent<StaminaComponent>(actor).StaminaDamage, Is.EqualTo(5f));
            Assert.That(entMan.GetComponent<DamageableComponent>(oldTarget).TotalDamage.Float(), Is.Zero);
            Assert.That(entMan.GetComponent<DamageableComponent>(newTarget).TotalDamage.Float(), Is.GreaterThan(0f));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DisablingTheVisibleWindupStillHonorsInputDelay()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var map = await pair.CreateTestMap();
        var entMan = pair.Server.EntMan;
        EntityUid actor = default;
        EntityUid target = default;
        await pair.Server.WaitAssertion(() =>
        {
            pair.Server.CfgMan.SetCVar(CVars.NetTickrate, 60);
            actor = entMan.SpawnEntity("LightAttackWindupTestActor", map.GridCoords);
            target = entMan.SpawnEntity("LightAttackWindupTestTarget", map.GridCoords);
            var light = entMan.GetComponent<LightAttackStaminaComponent>(actor);
            light.Windup = 0f;
            var melee = entMan.GetComponent<MeleeWeaponComponent>(actor);
            Assert.That(entMan.System<SharedMeleeWeaponSystem>().AttemptLightAttack(actor, actor, melee, target), Is.True);
            Assert.That(light.PendingAttack, Is.Not.Null);
            Assert.That(entMan.GetComponent<StaminaComponent>(actor).StaminaDamage, Is.Zero);
        });
        await pair.RunTicksSync(1);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<LightAttackStaminaComponent>(actor).PendingAttack, Is.Not.Null);
            Assert.That(entMan.GetComponent<StaminaComponent>(actor).StaminaDamage, Is.Zero);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<LightAttackStaminaComponent>(actor).PendingAttack, Is.Null);
            Assert.That(entMan.GetComponent<StaminaComponent>(actor).StaminaDamage, Is.EqualTo(5f));
            Assert.That(entMan.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.GreaterThan(0f));
        });
        await pair.CleanReturnAsync();
    }

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: LightAttackWindupTestActor
  components:
  - type: CombatMode
    isInCombatMode: true
  - type: Stamina
    decay: 0
  - type: MeleeWeapon
    attackRate: 1
    damage:
      types:
        Blunt: 10
  - type: LightAttackStamina
    staminaCost: 5

- type: entity
  id: LightAttackWindupTestTarget
  components:
  - type: Damageable
    damageContainer: Biological
";

    [Test]
    public async Task DelayAndPayOnceWithoutExtendingTheAttackPeriod()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var entMan = pair.Server.EntMan;
        EntityUid actor = default;
        EntityUid target = default;
        TimeSpan started = default;

        await pair.Server.WaitAssertion(() =>
        {
            actor = entMan.SpawnEntity("LightAttackWindupTestActor", map.GridCoords);
            target = entMan.SpawnEntity("LightAttackWindupTestTarget", map.GridCoords);
            var melee = entMan.GetComponent<MeleeWeaponComponent>(actor);
            var system = entMan.System<SharedMeleeWeaponSystem>();
            Assert.That(system.AttemptLightAttack(actor, actor, melee, target), Is.True);
            var windup = entMan.GetComponent<LightAttackStaminaComponent>(actor);
            started = windup.WindupStarted;
            var timing = pair.Server.ResolveDependency<IGameTiming>();
            var visual = entMan.GetComponent<LightAttackWindupVisualComponent>(windup.Effect!.Value);
            Assert.That((visual.Starts - started).TotalSeconds, Is.EqualTo(0.033).Within(1.0 / timing.TickRate));
            Assert.That((visual.Ends - visual.Starts).TotalSeconds, Is.EqualTo(0.45).Within(1.0 / timing.TickRate));
            Assert.That(windup.WindupEnds, Is.EqualTo(visual.Ends));
            Assert.That(visual.TryGetProgress(started, out _), Is.False);
            Assert.That(system.AttemptLightAttack(actor, actor, melee, target), Is.False);
            Assert.That(entMan.GetComponent<StaminaComponent>(actor).StaminaDamage, Is.Zero);
            Assert.That(entMan.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.Zero);
        });

        await pair.RunTicksSync(6);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<LightAttackStaminaComponent>(actor).PendingAttack, Is.Not.Null);
            Assert.That(entMan.GetComponent<StaminaComponent>(actor).StaminaDamage, Is.Zero);
            Assert.That(entMan.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.Zero);
        });

        await pair.RunTicksSync(12);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<LightAttackStaminaComponent>(actor).PendingAttack, Is.Null);
            Assert.That(entMan.GetComponent<StaminaComponent>(actor).StaminaDamage, Is.EqualTo(5f));
            Assert.That(entMan.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.GreaterThan(0f));
            Assert.That(entMan.GetComponent<MeleeWeaponComponent>(actor).NextAttack, Is.EqualTo(started + TimeSpan.FromSeconds(1)));
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CombatExitAndParryLockoutCancelBeforePayment(bool attackLockout)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var entMan = pair.Server.EntMan;
        EntityUid actor = default;
        EntityUid target = default;
        await pair.Server.WaitAssertion(() =>
        {
            actor = entMan.SpawnEntity("LightAttackWindupTestActor", map.GridCoords);
            target = entMan.SpawnEntity("LightAttackWindupTestTarget", map.GridCoords);
            var melee = entMan.GetComponent<MeleeWeaponComponent>(actor);
            Assert.That(entMan.System<SharedMeleeWeaponSystem>().AttemptLightAttack(actor, actor, melee, target), Is.True);
            if (attackLockout)
                melee.NextAttack = pair.Server.ResolveDependency<IGameTiming>().CurTime + TimeSpan.FromSeconds(2);
            else
                entMan.System<SharedCombatModeSystem>().SetInCombatMode(actor, false);
        });
        await pair.RunTicksSync(20);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<LightAttackStaminaComponent>(actor).PendingAttack, Is.Null);
            Assert.That(entMan.GetComponent<StaminaComponent>(actor).StaminaDamage, Is.Zero);
            Assert.That(entMan.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.Zero);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TargetLeavingRangeTurnsTheSwingIntoAPaidMiss()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var entMan = pair.Server.EntMan;
        EntityUid actor = default;
        EntityUid target = default;
        await pair.Server.WaitAssertion(() =>
        {
            actor = entMan.SpawnEntity("LightAttackWindupTestActor", map.GridCoords);
            target = entMan.SpawnEntity("LightAttackWindupTestTarget", map.GridCoords);
            var melee = entMan.GetComponent<MeleeWeaponComponent>(actor);
            Assert.That(entMan.System<SharedMeleeWeaponSystem>().AttemptLightAttack(actor, actor, melee, target), Is.True);
            entMan.System<SharedTransformSystem>().SetCoordinates(target, map.GridCoords.Offset(new Vector2(20, 0)));
        });
        await pair.RunTicksSync(20);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<StaminaComponent>(actor).StaminaDamage, Is.EqualTo(5f));
            Assert.That(entMan.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.Zero);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(0)] // Change to another target.
    [TestCase(1)] // Aim at empty space.
    [TestCase(2)] // Move the cursor beyond reach, even if a target ID is supplied.
    public async Task AimChangesRetargetOrMissWithoutRestartingTheWindup(int aimMode)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var entMan = pair.Server.EntMan;
        EntityUid actor = default;
        EntityUid originalTarget = default;
        EntityUid newTarget = default;
        TimeSpan deadline = default;
        TimeSpan started = default;

        await pair.Server.WaitAssertion(() =>
        {
            actor = entMan.SpawnEntity("LightAttackWindupTestActor", map.GridCoords);
            originalTarget = entMan.SpawnEntity("LightAttackWindupTestTarget", map.GridCoords.Offset(new Vector2(0.5f, 0)));
            newTarget = entMan.SpawnEntity("LightAttackWindupTestTarget", map.GridCoords.Offset(new Vector2(-0.5f, 0)));
            var melee = entMan.GetComponent<MeleeWeaponComponent>(actor);
            Assert.That(entMan.System<SharedMeleeWeaponSystem>().AttemptLightAttack(actor, actor, melee, originalTarget), Is.True);
            var component = entMan.GetComponent<LightAttackStaminaComponent>(actor);
            deadline = component.WindupEnds;
            started = component.WindupStarted;
        });

        await pair.RunTicksSync(4);
        await pair.Server.WaitAssertion(() =>
        {
            var system = entMan.System<LightAttackWindupSystem>();
            var component = entMan.GetComponent<LightAttackStaminaComponent>(actor);
            var id = component.PendingAttack!.WindupId;
            var coordinates = map.GridCoords.Offset(aimMode == 2 ? new Vector2(20, 0) : new Vector2(-0.5f, 0));
            var netCoordinates = entMan.GetNetCoordinates(coordinates);
            var netActor = entMan.GetNetEntity(actor);
            var netTarget = aimMode == 1 ? null : entMan.GetNetEntity((EntityUid?) newTarget);
            var aim = new LightAttackEvent(netTarget, netActor, netCoordinates, id);

            // Neither another actor nor a delayed update from another swing may retarget this one.
            Assert.That(system.TryUpdateAim(newTarget, aim), Is.False);
            Assert.That(system.TryUpdateAim(actor, new LightAttackEvent(netTarget, netActor, netCoordinates, unchecked(id + 1))), Is.False);
            Assert.That(system.TryUpdateAim(actor, aim), Is.True);
            Assert.That(component.WindupEnds, Is.EqualTo(deadline));
            Assert.That(component.WindupStarted, Is.EqualTo(started));
            Assert.That(component.PendingAttack!.Coordinates, Is.EqualTo(netCoordinates));
            Assert.That(component.PendingAttack.Target, aimMode == 0 ? Is.EqualTo(entMan.GetNetEntity(newTarget)) : Is.Null);
            Assert.That(entMan.GetComponent<StaminaComponent>(actor).StaminaDamage, Is.Zero);
        });

        await pair.RunTicksSync(14);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<LightAttackStaminaComponent>(actor).PendingAttack, Is.Null);
            Assert.That(entMan.GetComponent<StaminaComponent>(actor).StaminaDamage, Is.EqualTo(5f));
            Assert.That(entMan.GetComponent<DamageableComponent>(originalTarget).TotalDamage.Float(), Is.Zero);
            Assert.That(entMan.GetComponent<DamageableComponent>(newTarget).TotalDamage.Float(),
                aimMode == 0 ? Is.GreaterThan(0f) : Is.Zero);
            Assert.That(entMan.GetComponent<MeleeWeaponComponent>(actor).NextAttack, Is.EqualTo(started + TimeSpan.FromSeconds(1)));
            var late = new LightAttackEvent(entMan.GetNetEntity(newTarget), entMan.GetNetEntity(actor), entMan.GetNetCoordinates(map.GridCoords));
            Assert.That(entMan.System<LightAttackWindupSystem>().TryUpdateAim(actor, late), Is.False);
        });
        await pair.CleanReturnAsync();
    }
}
