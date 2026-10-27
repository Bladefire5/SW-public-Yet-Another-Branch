using Content.IntegrationTests.Pair;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Ghost;
using Content.Shared.Atmos.Components;
using Content.Shared.Light.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class EntityTimerMigrationTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ExperimentEntityTimerTest
  components:
  - type: Appearance
  - type: PoweredLight
    ghostBlinkingTime: 2
  - type: Flammable
    damage: {}
";

    [TestCase(false)]
    [TestCase(true)]
    public async Task TimersExpireAfterActiveTimeAndRetainGhostCooldown(bool pause)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        var second = (int) server.ResolveDependency<IGameTiming>().TickRate;
        EntityUid entity = default;

        await server.WaitAssertion(() => entity = StartTimers(pair, map.GridCoords));
        await pair.RunTicksSync(second / 2);

        if (pause)
        {
            await server.WaitPost(() => server.System<MetaDataSystem>().SetEntityPaused(entity, true));
            await pair.RunTicksSync(second * 3);
            await server.WaitAssertion(() => AssertActive(entities, entity, true));
            await server.WaitPost(() => server.System<MetaDataSystem>().SetEntityPaused(entity, false));
        }

        await pair.RunTicksSync(second);
        await server.WaitAssertion(() => AssertActive(entities, entity, true));
        await pair.RunTicksSync(second);
        await server.WaitAssertion(() =>
        {
            AssertActive(entities, entity, false);

            var boo = new GhostBooEvent();
            entities.EventBus.RaiseLocalEvent(entity, boo);
            Assert.That(boo.Handled, Is.False, "Finishing a blink must not reset its cooldown.");
            Assert.That(entities.GetComponent<PoweredLightComponent>(entity).IsBlinking, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletingEntityWithPendingTimersLeavesOtherEntitiesWorking()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var second = (int) server.ResolveDependency<IGameTiming>().TickRate;
        EntityUid deleted = default;
        EntityUid survivor = default;

        await server.WaitAssertion(() =>
        {
            deleted = StartTimers(pair, map.GridCoords);
            survivor = StartTimers(pair, map.GridCoords);
            server.EntMan.DeleteEntity(deleted);
        });

        await pair.RunTicksSync(second * 3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(deleted), Is.False);
            AssertActive(server.EntMan, survivor, false);
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid StartTimers(TestPair pair, EntityCoordinates coordinates)
    {
        var entities = pair.Server.EntMan;
        var entity = entities.SpawnEntity("ExperimentEntityTimerTest", coordinates);
        var boo = new GhostBooEvent();
        entities.EventBus.RaiseLocalEvent(entity, boo);
        Assert.That(boo.Handled, Is.True);

        // Zero stacks keep the fire test independent of atmospheric damage and fire fading.
        var flammable = entities.GetComponent<FlammableComponent>(entity);
        flammable.OnFire = true;
        pair.Server.System<FlammableSystem>().Resist(entity, flammable);
        AssertActive(entities, entity, true);
        return entity;
    }

    private static void AssertActive(IEntityManager entities, EntityUid entity, bool active)
    {
        Assert.Multiple(() =>
        {
            Assert.That(entities.GetComponent<PoweredLightComponent>(entity).IsBlinking, Is.EqualTo(active));
            Assert.That(entities.GetComponent<FlammableComponent>(entity).Resisting, Is.EqualTo(active));
        });
    }
}
