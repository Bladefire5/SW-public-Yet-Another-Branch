using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Medieval.Bee.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class BeeHiveLifecycleMigrationTest
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task RemovingHiveReleasesItsMapAndEvacuatesOccupants(bool deleteOwner)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        EntityUid hive = default;
        EntityUid interiorMap = default;
        EntityUid interiorGrid = default;
        EntityUid occupant = default;
        EntityUid resident = default;

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            hive = entities.SpawnEntity("MedievalBeeHive", map.GridCoords);
            var component = entities.GetComponent<MedievalBeeHiveComponent>(hive);
            interiorMap = component.InteriorMap!.Value;
            interiorGrid = component.Grid.Owner;
            occupant = entities.SpawnEntity("MobHuman", new EntityCoordinates(interiorGrid, Vector2.Zero));
            var spawner = entities.EntityQuery<MedievalBeeLinkedSpawnerComponent>(true)
                .First(candidate => entities.GetComponent<TransformComponent>(candidate.Owner).GridUid == interiorGrid);
            resident = entities.SpawnEntity("MobHuman", new EntityCoordinates(interiorGrid, Vector2.Zero));
            entities.EnsureComponent<MedievalBeeLinkedMobComponent>(resident).LinkedSpawner = (spawner.Owner, spawner);

            if (deleteOwner)
                entities.DeleteEntity(hive);
            else
                entities.RemoveComponent<MedievalBeeHiveComponent>(hive);

            Assert.That(entities.GetComponent<TransformComponent>(occupant).GridUid, Is.EqualTo(map.Grid.Owner));
        });

        await pair.Server.WaitRunTicks(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.Deleted(interiorMap), Is.True);
            Assert.That(entities.Deleted(interiorGrid), Is.True);
            Assert.That(entities.Deleted(occupant), Is.False);
            Assert.That(entities.Deleted(resident), Is.True);
            if (!deleteOwner)
                entities.DeleteEntity(hive);
            entities.DeleteEntity(occupant);
        });
        await pair.CleanReturnAsync();
    }
}
