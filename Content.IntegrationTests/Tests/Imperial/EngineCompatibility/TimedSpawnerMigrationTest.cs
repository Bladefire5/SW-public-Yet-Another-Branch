using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class TimedSpawnerMigrationTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ExperimentTimedSpawnMarker
- type: entity
  id: ExperimentTimedSpawner
  components:
  - type: TimedSpawner
    prototypes: [ExperimentTimedSpawnMarker]
    intervalSeconds: 1
    chance: 1
    minimumEntitiesSpawned: 1
    maximumEntitiesSpawned: 1
";

    [TestCase(false)]
    [TestCase(true)]
    public async Task SpawnerUsesOwnDeadlineRespectsPauseAndStopsOnDeletion(bool pause)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        var second = (int) server.ResolveDependency<IGameTiming>().TickRate;
        EntityUid spawner = default;

        int CountSpawned() => entities.EntityQuery<MetaDataComponent>()
            .Count(meta => meta.EntityPrototype?.ID == "ExperimentTimedSpawnMarker");

        await server.WaitPost(() => spawner = entities.SpawnEntity("ExperimentTimedSpawner", map.GridCoords));
        await pair.RunTicksSync(second / 2);
        await server.WaitAssertion(() => Assert.That(CountSpawned(), Is.Zero));

        if (pause)
        {
            await server.WaitPost(() => server.System<MetaDataSystem>().SetEntityPaused(spawner, true));
            await pair.RunTicksSync(second * 2);
            await server.WaitAssertion(() => Assert.That(CountSpawned(), Is.Zero));
            await server.WaitPost(() => server.System<MetaDataSystem>().SetEntityPaused(spawner, false));
        }

        await pair.RunTicksSync(second * 3 / 4);
        await server.WaitAssertion(() => Assert.That(CountSpawned(), Is.EqualTo(1)));
        await pair.RunTicksSync(second);
        await server.WaitAssertion(() =>
        {
            Assert.That(CountSpawned(), Is.EqualTo(2));
            entities.DeleteEntity(spawner);
        });
        await pair.RunTicksSync(second * 2);
        await server.WaitAssertion(() => Assert.That(CountSpawned(), Is.EqualTo(2)));

        await pair.CleanReturnAsync();
    }
}
