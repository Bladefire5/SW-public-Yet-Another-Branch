using Content.Server.GameTicking;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class RoundMapCleanupMigrationTest
{
    [Test]
    public async Task RepeatedRoundRestartsRemoveOldMapsAndAllowNewGrids()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Dirty = true });
        var server = pair.Server;

        for (var restart = 0; restart < 2; restart++)
        {
            var map = await pair.CreateTestMap();
            await server.WaitAssertion(() =>
            {
                var maps = server.System<SharedMapSystem>();
                Assert.That(maps.MapExists(map.MapId), Is.True);
                Assert.That(server.EntMan.EntityExists(map.Grid), Is.True);

                server.System<GameTicker>().RestartRound();

                Assert.That(maps.MapExists(map.MapId), Is.False, "Round cleanup must remove the map registry entry.");
                Assert.That(server.EntMan.EntityExists(map.MapUid), Is.False);
                Assert.That(server.EntMan.EntityExists(map.Grid), Is.False);
            });
        }

        // Also exercise the tile API on a newly created grid after both resets.
        var fresh = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var maps = server.System<SharedMapSystem>();
            Assert.That(maps.GetMap(fresh.MapId), Is.EqualTo(fresh.MapUid));
            Assert.That(maps.GetTileRef(fresh.Grid, fresh.Grid.Comp, fresh.GridCoords).Tile, Is.EqualTo(fresh.Tile.Tile));
        });

        await pair.CleanReturnAsync();
    }
}
