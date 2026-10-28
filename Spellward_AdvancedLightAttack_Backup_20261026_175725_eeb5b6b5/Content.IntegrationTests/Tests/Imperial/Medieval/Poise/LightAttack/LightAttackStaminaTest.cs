// spellward - restore the stamina-only version, before light attack preparation.
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Imperial.Medieval.Poise.LightAttack;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.Poise.LightAttack;

[TestFixture]
public sealed class LightAttackStaminaTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: lightAttackStaminaPreset
  id: LightAttackStaminaTestHeavy
  staminaCost: 12

- type: entity
  id: LightAttackStaminaTestAttacker
  components:
  - type: CombatMode
    isInCombatMode: true
  - type: Stamina
    decay: 0
  - type: Poise
  - type: StaminaResistance
    damageCoefficient: 0

- type: entity
  id: LightAttackStaminaTestWeapon
  components:
  - type: MeleeWeapon
    damage:
      types:
        Blunt: 10
  - type: PoiseDamage
  - type: LightAttackStamina
    staminaCost: 5

- type: entity
  id: LightAttackStaminaTestTarget
  components:
  - type: Damageable
    damageContainer: Biological
";

    [Test]
    public async Task PresetsAndPerWeaponOverrides()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var entMan = pair.Server.EntMan;

        await pair.Server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity("LightAttackStaminaTestAttacker", map.GridCoords);
            var weapon = entMan.SpawnEntity("LightAttackStaminaTestWeapon", map.GridCoords);
            var component = entMan.GetComponent<LightAttackStaminaComponent>(weapon);
            var system = entMan.System<LightAttackStaminaSystem>();

            component.StaminaCost = null;
            Assert.That(system.GetStaminaCost(weapon), Is.EqualTo(5f));

            component.Preset = "LightAttackStaminaTestHeavy";
            Assert.That(system.GetStaminaCost(weapon), Is.EqualTo(12f));

            component.StaminaCost = 3f;
            Assert.That(system.GetStaminaCost(weapon), Is.EqualTo(3f));

            component.StaminaCost = 0f;
            Assert.That(system.TrySpendStamina(user, weapon), Is.True);
            Assert.That(entMan.GetComponent<StaminaComponent>(user).StaminaDamage, Is.Zero);

            component.StaminaCost = null;
            Assert.That(system.GetStaminaCost(weapon), Is.EqualTo(12f));

            var unconfiguredWeapon = entMan.SpawnEntity(null, map.GridCoords);
            Assert.That(system.GetStaminaCost(unconfiguredWeapon), Is.Zero);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(4f, true)]
    [TestCase(4f, false)]
    [TestCase(11f, true)]
    [TestCase(11f, false)]
    public async Task LightSwingsSpendWeaponCost(float cost, bool hit)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var entMan = pair.Server.EntMan;

        await pair.Server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity("LightAttackStaminaTestAttacker", map.GridCoords);
            var weapon = entMan.SpawnEntity("LightAttackStaminaTestWeapon", map.GridCoords);
            var target = entMan.SpawnEntity("LightAttackStaminaTestTarget", map.GridCoords);
            var melee = entMan.GetComponent<MeleeWeaponComponent>(weapon);
            var stamina = entMan.GetComponent<StaminaComponent>(user);
            entMan.GetComponent<LightAttackStaminaComponent>(weapon).StaminaCost = cost;
            var system = entMan.System<SharedMeleeWeaponSystem>();

            if (hit)
                Assert.That(system.AttemptLightAttack(user, weapon, melee, target), Is.True);
            else
                system.AttemptLightAttackMiss(user, weapon, melee, map.GridCoords);

            // The attacker has both poise and complete stamina resistance. Neither
            // should erase the cost, including when the weapon also deals poise damage.
            Assert.That(stamina.StaminaDamage, Is.EqualTo(cost));
            Assert.That(melee.Attacking, Is.True);
            Assert.That(entMan.GetComponent<DamageableComponent>(target).TotalDamage.Float(),
                hit ? Is.GreaterThan(0f) : Is.EqualTo(0f));

            // Repeating the request during cooldown must not charge a second time.
            system.AttemptLightAttackMiss(user, weapon, melee, map.GridCoords);
            Assert.That(stamina.StaminaDamage, Is.EqualTo(cost));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RejectedSwingsSpendNothingAndDealNoDamage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var entMan = pair.Server.EntMan;

        await pair.Server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity("LightAttackStaminaTestAttacker", map.GridCoords);
            var weapon = entMan.SpawnEntity("LightAttackStaminaTestWeapon", map.GridCoords);
            var target = entMan.SpawnEntity("LightAttackStaminaTestTarget", map.GridCoords);
            var melee = entMan.GetComponent<MeleeWeaponComponent>(weapon);
            var stamina = entMan.GetComponent<StaminaComponent>(user);
            var system = entMan.System<SharedMeleeWeaponSystem>();
            var combat = entMan.System<SharedCombatModeSystem>();

            combat.SetInCombatMode(user, false);
            Assert.That(system.AttemptLightAttack(user, weapon, melee, target), Is.False);
            Assert.That(stamina.StaminaDamage, Is.Zero);
            combat.SetInCombatMode(user, true);

            // A weapon rejected by another attack check must not spend stamina.
            entMan.AddComponent<WieldableComponent>(weapon);
            entMan.AddComponent<MeleeRequiresWieldComponent>(weapon);
            Assert.That(system.AttemptLightAttack(user, weapon, melee, target), Is.False);
            Assert.That(stamina.StaminaDamage, Is.Zero);
            entMan.RemoveComponent<MeleeRequiresWieldComponent>(weapon);
            melee.NextAttack = TimeSpan.Zero;

            // Match TryTakeStamina: spending may not reach the exhaustion threshold.
            stamina.StaminaDamage = stamina.CritThreshold - 5f;
            Assert.That(system.AttemptLightAttack(user, weapon, melee, target), Is.False);
            Assert.That(stamina.StaminaDamage, Is.EqualTo(stamina.CritThreshold - 5f));
            Assert.That(stamina.Critical, Is.False);
            Assert.That(melee.Attacking, Is.False);
            Assert.That(entMan.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.Zero);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeavySwingsDoNotSpendLightAttackStamina()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var entMan = pair.Server.EntMan;

        await pair.Server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity("LightAttackStaminaTestAttacker", map.GridCoords);
            var weapon = entMan.SpawnEntity("LightAttackStaminaTestWeapon", map.GridCoords);
            var melee = entMan.GetComponent<MeleeWeaponComponent>(weapon);
            var stamina = entMan.GetComponent<StaminaComponent>(user);

            Assert.That(entMan.System<SharedMeleeWeaponSystem>()
                .AttemptHeavyAttack(user, weapon, melee, map.GridCoords), Is.True);
            Assert.That(stamina.StaminaDamage, Is.Zero);
        });

        await pair.CleanReturnAsync();
    }
}
