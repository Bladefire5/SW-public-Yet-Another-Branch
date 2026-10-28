// spellward - minimum charge, commitment cost, scaling and full post-strike recovery.
using System.Numerics;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.ChargedAttack;
using Content.Shared.Imperial.Medieval.Poise.LightAttack;
using Content.Shared.Weapons.Melee;
using Robust.Shared;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.Poise.LightAttack;

[TestFixture]
public sealed class AdvancedChargedAttackTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: AdvancedChargeTestUser
  components:
  - type: CombatMode
    isInCombatMode: true
  - type: Hands
  - type: Stamina
    decay: 0

- type: entity
  id: AdvancedChargeTestWeapon
  components:
  - type: Item
  - type: MeleeWeapon
    resetOnHandSelected: false
    attackRate: 1
    damage:
      types:
        Blunt: 20
  - type: AdvancedLightAttack
  - type: ChargedAttack
    maxAttackTime: 4
    staticModifier: 1.5
    staminaDamage: 10
    effectProtoId: AttackEffect4s
";

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(128)]
    public async Task TapWaitsForMinimumAndPaysOnceAtCommitment(int tickRate)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid user = default;
        EntityUid weapon = default;
        TimeSpan minimum = default;
        await pair.Server.WaitAssertion(() =>
        {
            pair.Server.CfgMan.SetCVar(CVars.NetTickrate, tickRate);
            user = entities.SpawnEntity("AdvancedChargeTestUser", map.GridCoords);
            weapon = entities.SpawnEntity("AdvancedChargeTestWeapon", map.GridCoords);
            var hands = entities.System<SharedHandsSystem>();
            var handComponent = entities.GetComponent<HandsComponent>(user);
            hands.AddHand((user, handComponent), "right", HandLocation.Right);
            Assert.That(hands.TryPickup(user, weapon, "right", animate: false), Is.True);
            var aim = entities.GetNetCoordinates(new EntityCoordinates(map.GridCoords.EntityId,
                map.GridCoords.Position + Vector2.UnitX));
            var system = entities.System<AdvancedLightAttackSystem>();
            Assert.That(system.TryBeginCharged(user, weapon, entities.GetComponent<MeleeWeaponComponent>(weapon), aim), Is.True);
            var state = entities.GetComponent<AdvancedLightAttackComponent>(weapon);
            Assert.That(system.TryReleaseCharge(user,
                new AdvancedChargedAttackReleaseEvent(entities.GetNetEntity(weapon), state.AttackId, aim)), Is.True);
            minimum = state.MinimumStrike;
            Assert.That(state.Strikes, Is.EqualTo(minimum));
            Assert.That(system.GetChargeWindup(state), Is.EqualTo(0.6f));
            Assert.That(entities.GetComponent<StaminaComponent>(user).StaminaDamage, Is.Zero);
            Assert.That(entities.GetComponent<MeleeWeaponComponent>(weapon).NextAttack, Is.EqualTo(TimeSpan.Zero));
        });

        await pair.RunTicksSync((int) MathF.Ceiling(AdvancedLightAttackSystem.InputDelay * tickRate) + 1);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<AdvancedLightAttackComponent>(weapon).Phase,
                Is.EqualTo(AdvancedLightAttackPhase.Windup));
            Assert.That(entities.GetComponent<StaminaComponent>(user).StaminaDamage, Is.EqualTo(10f));
            Assert.That(entities.GetComponent<MeleeWeaponComponent>(weapon).NextAttack, Is.EqualTo(TimeSpan.Zero));
        });

        await pair.RunTicksSync((int) MathF.Ceiling(0.6f * tickRate) + 2);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<AdvancedLightAttackComponent>(weapon).Phase,
                Is.EqualTo(AdvancedLightAttackPhase.Recovery));
            Assert.That(entities.GetComponent<MeleeWeaponComponent>(weapon).NextAttack,
                Is.GreaterThanOrEqualTo(minimum + TimeSpan.FromSeconds(1)));
            Assert.That(entities.GetComponent<StaminaComponent>(user).StaminaDamage, Is.EqualTo(10f));
            Assert.That(entities.GetComponent<ChargedAttackComponent>(weapon).CurrentAttacking, Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(0f, 0.1f, 0.5f)]
    [TestCase(0.5f, 0.55f, 0.75f)]
    [TestCase(1f, 1f, 1f)]
    public void ChargeBonusFractions(float progress, float damage, float poise)
    {
        var minimum = TimeSpan.FromSeconds(0.6);
        var maximum = TimeSpan.FromSeconds(4);
        var strike = minimum + (maximum - minimum) * progress;
        var result = AdvancedLightAttackSystem.GetChargeFractions(strike, minimum, maximum);
        Assert.Multiple(() =>
        {
            Assert.That(result.Damage, Is.EqualTo(damage).Within(0.00001f));
            Assert.That(result.Poise, Is.EqualTo(poise).Within(0.00001f));
        });
    }
}
