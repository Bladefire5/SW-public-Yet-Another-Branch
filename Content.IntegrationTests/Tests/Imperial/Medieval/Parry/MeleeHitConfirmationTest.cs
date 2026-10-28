using Content.Shared.CCVar;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Poise;
using Content.Shared.MeleeParry;
using Content.Shared.MeleeParry.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.Parry;

[TestFixture]
public sealed class MeleeHitConfirmationTest
{
    // spellward - successful catches leave the same parry active for later attackers.
    [Test]
    public async Task OneParryCatchesSuccessiveImpactsThroughoutItsWindow()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid defender = default;
        EntityUid weapon = default;
        var attackers = new EntityUid[3];
        TimeSpan starts = default;
        await pair.Server.WaitAssertion(() =>
        {
            pair.Server.CfgMan.SetCVar(CVars.NetTickrate, 60);
            pair.Server.CfgMan.SetCVar(CCVars.ParryConfirmationBuffer, 0.05f);
            defender = entities.SpawnEntity("ParryConfirmationTestDefender", map.GridCoords);
            weapon = entities.SpawnEntity("ParryConfirmationTestWeapon", map.GridCoords);
            var hands = entities.System<SharedHandsSystem>();
            var component = entities.GetComponent<HandsComponent>(defender);
            hands.AddHand((defender, component), "right", HandLocation.Right);
            Assert.That(hands.TryPickup(defender, weapon, "right", animate: false), Is.True);
            for (var i = 0; i < attackers.Length; i++)
                attackers[i] = entities.SpawnEntity("ParryConfirmationTestAttacker", map.GridCoords);
            var timing = pair.Server.ResolveDependency<IGameTiming>();
            Assert.That(entities.System<MeleeParrySystem>().TryStartParry(defender, weapon, timing.CurTick), Is.True);
            starts = entities.GetComponent<MeleeParryComponent>(weapon).ParriedTime;
        });
        foreach (var attacker in attackers)
        {
            await pair.Server.WaitAssertion(() =>
            {
                var melee = entities.GetComponent<MeleeWeaponComponent>(attacker);
                Assert.That(entities.System<SharedMeleeWeaponSystem>().AttemptLightAttack(attacker, attacker, melee, defender), Is.True);
            });
            await pair.RunTicksSync(5);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(entities.GetComponent<DamageableComponent>(defender).TotalDamage.Float(), Is.Zero);
                Assert.That(entities.GetComponent<PoiseComponent>(defender).PoiseDamage, Is.Zero);
                Assert.That(entities.GetComponent<MeleeParryComponent>(weapon).ParriedTime, Is.EqualTo(starts));
            });
        }
        await pair.CleanReturnAsync();
    }

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ParryConfirmationTestAttacker
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
        ParryAble: 1
  - type: PoiseDamage
    poiseDamage: 15

- type: entity
  id: ParryConfirmationTestDefender
  components:
  - type: CombatMode
    isInCombatMode: true
  - type: Hands
  - type: Damageable
    damageContainer: Biological
  - type: Poise
  - type: MeleeParryAble
  - type: MeleeParryStorage

- type: entity
  id: ParryConfirmationTestWeapon
  components:
  - type: Item
  - type: MeleeWeapon
    damage:
      types:
        Blunt: 1
        ParryAble: 1
  - type: MeleeParry
";

    [TestCase(false)]
    [TestCase(true)]
    public async Task LateDeliveryUsesInputTimeInsteadOfConfirmationTime(bool pressedAfterImpact)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var map = await pair.CreateTestMap();
        var entMan = pair.Server.EntMan;
        EntityUid attacker = default;
        EntityUid defender = default;
        EntityUid weapon = default;
        GameTick impactTick = default;

        await pair.Server.WaitAssertion(() =>
        {
            pair.Server.CfgMan.SetCVar(CVars.NetTickrate, 60);
            pair.Server.CfgMan.SetCVar(CCVars.ParryConfirmationBuffer, 0.05f);
            attacker = entMan.SpawnEntity("ParryConfirmationTestAttacker", map.GridCoords);
            defender = entMan.SpawnEntity("ParryConfirmationTestDefender", map.GridCoords);
            weapon = entMan.SpawnEntity("ParryConfirmationTestWeapon", map.GridCoords);
            var hands = entMan.System<SharedHandsSystem>();
            var component = entMan.GetComponent<HandsComponent>(defender);
            hands.AddHand((defender, component), "right", HandLocation.Right);
            Assert.That(hands.TryPickup(defender, weapon, "right", animate: false), Is.True);

            var timing = pair.Server.ResolveDependency<IGameTiming>();
            impactTick = timing.CurTick;
            var melee = entMan.GetComponent<MeleeWeaponComponent>(attacker);
            Assert.That(entMan.System<SharedMeleeWeaponSystem>().AttemptLightAttack(attacker, attacker, melee, defender), Is.True);
            Assert.That(entMan.GetComponent<DamageableComponent>(defender).TotalDamage.Float(), Is.Zero);
            Assert.That(entMan.GetComponent<PoiseComponent>(defender).PoiseDamage, Is.Zero);
        });

        await pair.RunTicksSync(1);
        await pair.Server.WaitAssertion(() =>
        {
            var timing = pair.Server.ResolveDependency<IGameTiming>();
            var inputTick = pressedAfterImpact ? timing.CurTick : impactTick;
            Assert.That(entMan.System<MeleeParrySystem>().TryStartParry(defender, weapon, inputTick), Is.True);
            Assert.That(entMan.GetComponent<DamageableComponent>(defender).TotalDamage.Float(), Is.Zero);
            Assert.That(entMan.GetComponent<PoiseComponent>(defender).PoiseDamage, Is.Zero);
        });

        await pair.RunTicksSync(6);
        await pair.Server.WaitAssertion(() =>
        {
            var damage = entMan.GetComponent<DamageableComponent>(defender).TotalDamage.Float();
            var poise = entMan.GetComponent<PoiseComponent>(defender).PoiseDamage;
            if (pressedAfterImpact)
            {
                Assert.That(damage, Is.GreaterThan(0f));
                Assert.That(poise, Is.EqualTo(15));
            }
            else
            {
                Assert.That(damage, Is.Zero);
                Assert.That(poise, Is.Zero);
                Assert.That(entMan.GetComponent<MeleeParryStorageComponent>(defender).NextParryTime, Is.EqualTo(TimeSpan.Zero));
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PoiseUsesTheContactSnapshotAndUnparriedDamageCommitsOnlyOnce()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var map = await pair.CreateTestMap();
        var entMan = pair.Server.EntMan;
        EntityUid attacker = default;
        EntityUid defender = default;
        float confirmedDamage = 0f;
        await pair.Server.WaitAssertion(() =>
        {
            pair.Server.CfgMan.SetCVar(CVars.NetTickrate, 60);
            pair.Server.CfgMan.SetCVar(CCVars.ParryConfirmationBuffer, 0.05f);
            attacker = entMan.SpawnEntity("ParryConfirmationTestAttacker", map.GridCoords);
            defender = entMan.SpawnEntity("ParryConfirmationTestDefender", map.GridCoords);
            var weapon = entMan.SpawnEntity("ParryConfirmationTestWeapon", map.GridCoords);
            var hands = entMan.System<SharedHandsSystem>();
            var component = entMan.GetComponent<HandsComponent>(defender);
            hands.AddHand((defender, component), "right", HandLocation.Right);
            Assert.That(hands.TryPickup(defender, weapon, "right", animate: false), Is.True);
            var melee = entMan.GetComponent<MeleeWeaponComponent>(attacker);
            Assert.That(entMan.System<SharedMeleeWeaponSystem>().AttemptLightAttack(attacker, attacker, melee, defender), Is.True);
            entMan.GetComponent<PoiseDamageComponent>(attacker).PoiseDamage = 30;
            Assert.That(entMan.GetComponent<PoiseComponent>(defender).PoiseDamage, Is.Zero);
        });
        await pair.RunTicksSync(6);
        await pair.Server.WaitAssertion(() =>
        {
            confirmedDamage = entMan.GetComponent<DamageableComponent>(defender).TotalDamage.Float();
            Assert.That(confirmedDamage, Is.GreaterThan(0f));
            Assert.That(entMan.GetComponent<PoiseComponent>(defender).PoiseDamage, Is.EqualTo(15));
        });
        await pair.RunTicksSync(6);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<DamageableComponent>(defender).TotalDamage.Float(), Is.EqualTo(confirmedDamage));
            Assert.That(entMan.GetComponent<PoiseComponent>(defender).PoiseDamage, Is.EqualTo(15));
        });
        await pair.CleanReturnAsync();
    }
}
