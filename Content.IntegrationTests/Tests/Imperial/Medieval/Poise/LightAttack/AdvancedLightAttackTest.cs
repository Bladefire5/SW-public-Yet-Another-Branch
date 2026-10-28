using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Poise.LightAttack;
using Content.Shared.MeleeParry;
using Content.Shared.Weapons.Melee;
using Robust.Shared;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.Poise.LightAttack;

/// <summary>spellward: preparation, commitment, independent hands and parry cancellation.</summary>
[TestFixture]
public sealed class AdvancedLightAttackTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: advancedLightAttackPreset
  id: AdvancedLightTestPreset
  staminaCost: 12
  windup: 0.6

- type: entity
  id: AdvancedLightTestUser
  components:
  - type: CombatMode
    isInCombatMode: true
  - type: Hands
  - type: Stamina
    decay: 0
  - type: Poise
  - type: StaminaResistance
    damageCoefficient: 0
  - type: MeleeParryStorage

- type: entity
  id: AdvancedLightTestWeapon
  components:
  - type: Item
  - type: MeleeWeapon
    resetOnHandSelected: false
    attackRate: 1
    damage:
      types:
        Blunt: 10
  - type: AdvancedLightAttack
  - type: MeleeParry

- type: entity
  id: AdvancedLightTestTarget
  components:
  - type: Damageable
    damageContainer: Biological
";

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(128)]
    public async Task CostAtCommitmentAndFullRecoveryAfterStrike(int tickRate)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid user = default;
        EntityUid weapon = default;
        EntityUid target = default;
        TimeSpan struck = default;

        await pair.Server.WaitAssertion(() =>
        {
            pair.Server.CfgMan.SetCVar(CVars.NetTickrate, tickRate);
            user = entities.SpawnEntity("AdvancedLightTestUser", map.GridCoords);
            weapon = entities.SpawnEntity("AdvancedLightTestWeapon", map.GridCoords);
            target = entities.SpawnEntity("AdvancedLightTestTarget", map.GridCoords);
            var hands = entities.System<SharedHandsSystem>();
            var component = entities.GetComponent<HandsComponent>(user);
            hands.AddHand((user, component), "right", HandLocation.Right);
            Assert.That(hands.TryPickup(user, weapon, "right", animate: false), Is.True);
            var melee = entities.GetComponent<MeleeWeaponComponent>(weapon);
            Assert.That(entities.System<SharedMeleeWeaponSystem>().AttemptLightAttack(user, weapon, melee, target), Is.True);
            var light = entities.GetComponent<AdvancedLightAttackComponent>(weapon);
            Assert.That(light.Phase, Is.EqualTo(AdvancedLightAttackPhase.Preparing));
            Assert.That(entities.GetComponent<StaminaComponent>(user).StaminaDamage, Is.Zero);
            Assert.That(entities.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.Zero);
            Assert.That(melee.NextAttack, Is.EqualTo(TimeSpan.Zero));
        });

        await pair.RunTicksSync((int) MathF.Ceiling(AdvancedLightAttackSystem.InputDelay * tickRate) + 1);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<AdvancedLightAttackComponent>(weapon).Phase,
                Is.EqualTo(AdvancedLightAttackPhase.Windup));
            Assert.That(entities.GetComponent<StaminaComponent>(user).StaminaDamage, Is.EqualTo(5f));
            Assert.That(entities.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.Zero);
            var melee = entities.GetComponent<MeleeWeaponComponent>(weapon);
            Assert.That(entities.System<SharedMeleeWeaponSystem>().AttemptLightAttack(user, weapon, melee, target), Is.False);
            Assert.That(entities.GetComponent<StaminaComponent>(user).StaminaDamage, Is.EqualTo(5f));
        });

        await pair.RunTicksSync((int) MathF.Ceiling(0.4f * tickRate) + 2);
        await pair.Server.WaitAssertion(() =>
        {
            var light = entities.GetComponent<AdvancedLightAttackComponent>(weapon);
            var melee = entities.GetComponent<MeleeWeaponComponent>(weapon);
            Assert.That(light.Phase, Is.EqualTo(AdvancedLightAttackPhase.Recovery));
            Assert.That(entities.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.GreaterThan(0f));
            Assert.That(entities.GetComponent<StaminaComponent>(user).StaminaDamage, Is.EqualTo(5f));
            struck = light.Strikes;
            Assert.That(melee.NextAttack, Is.GreaterThanOrEqualTo(struck + TimeSpan.FromSeconds(1f)));
            Assert.That(entities.System<SharedMeleeWeaponSystem>().AttemptLightAttack(user, weapon, melee, target), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task BothHandsRemainPendingAndParryCancelsThemWithoutRefund(bool committed)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid user = default;
        EntityUid first = default;
        EntityUid second = default;
        await pair.Server.WaitAssertion(() =>
        {
            user = entities.SpawnEntity("AdvancedLightTestUser", map.GridCoords);
            first = entities.SpawnEntity("AdvancedLightTestWeapon", map.GridCoords);
            second = entities.SpawnEntity("AdvancedLightTestWeapon", map.GridCoords);
            var target = entities.SpawnEntity("AdvancedLightTestTarget", map.GridCoords);
            var hands = entities.System<SharedHandsSystem>();
            var component = entities.GetComponent<HandsComponent>(user);
            hands.AddHand((user, component), "right", HandLocation.Right);
            hands.AddHand((user, component), "left", HandLocation.Left);
            Assert.That(hands.TryPickup(user, first, "right", animate: false), Is.True);
            Assert.That(hands.TryPickup(user, second, "left", animate: false), Is.True);
            var melee = entities.System<SharedMeleeWeaponSystem>();
            hands.SetActiveHand((user, component), "right");
            Assert.That(melee.AttemptLightAttack(user, first, entities.GetComponent<MeleeWeaponComponent>(first), target), Is.True);
            var deadline = entities.GetComponent<AdvancedLightAttackComponent>(first).Strikes;
            Assert.That(hands.SetActiveHand((user, component), "left"), Is.True);
            Assert.That(melee.AttemptLightAttack(user, second, entities.GetComponent<MeleeWeaponComponent>(second), target), Is.True);
            Assert.That(entities.GetComponent<AdvancedLightAttackComponent>(first).Strikes, Is.EqualTo(deadline));
            Assert.That(entities.GetComponent<AdvancedLightAttackUserComponent>(user).IsDualWielding, Is.True);
        });
        if (committed)
        {
            var timing = pair.Server.ResolveDependency<IGameTiming>();
            await pair.RunTicksSync((int) MathF.Ceiling(AdvancedLightAttackSystem.InputDelay * timing.TickRate) + 1);
        }
        await pair.Server.WaitAssertion(() =>
        {
            var timing = pair.Server.ResolveDependency<IGameTiming>();
            Assert.That(entities.System<MeleeParrySystem>().TryStartParry(user, second, timing.CurTick), Is.True);
            foreach (var weapon in new[] { first, second })
            {
                Assert.That(entities.GetComponent<AdvancedLightAttackComponent>(weapon).Phase,
                    Is.EqualTo(AdvancedLightAttackPhase.Idle));
                Assert.That(entities.GetComponent<AdvancedLightAttackComponent>(weapon).PendingAttack, Is.Null);
            }
            Assert.That(entities.GetComponent<StaminaComponent>(user).StaminaDamage, Is.EqualTo(committed ? 10f : 0f));
            Assert.That(entities.System<AdvancedLightAttackSystem>().IsAttackLocked(user), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PresetAndWeaponOverridesShareOneComponent()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var weapon = entities.SpawnEntity("AdvancedLightTestWeapon", map.GridCoords);
            var component = entities.GetComponent<AdvancedLightAttackComponent>(weapon);
            var system = entities.System<AdvancedLightAttackSystem>();
            component.Preset = "AdvancedLightTestPreset";
            Assert.That(system.GetStaminaCost(component), Is.EqualTo(12f));
            Assert.That(system.GetWindup(component), Is.EqualTo(0.6f));
            component.StaminaCost = 3f;
            component.Windup = 0.2f;
            component.WindupEffect = "MedievalAdvancedLightAttackCue";
            Assert.That(system.GetStaminaCost(component), Is.EqualTo(3f));
            Assert.That(system.GetWindup(component), Is.EqualTo(0.2f));
            Assert.That(system.GetEffect(component).Id, Is.EqualTo("MedievalAdvancedLightAttackCue"));
        });
        await pair.CleanReturnAsync();
    }
}
