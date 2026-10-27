// spellward start
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Shared.Administration;
using Content.Shared.Damage;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Explosion.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using ServerExplosions = Content.Server.Explosion.EntitySystems.ExplosionSystem;
using ClientExplosions = Content.Client.Explosion.EntitySystems.ExplosionSystem;

namespace Content.IntegrationTests.Tests.Projectiles;

[TestFixture, NonParallelizable]
public sealed class ProjectileExplosionWallsTest
{
    private sealed record Scene(Entity<MapGridComponent> Grid, MapId Map, MapCoordinates Origin, EntityUid Center);

    private static async Task<Scene> Barrier(TestPair pair, string layout = "solid")
    {
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        EntityUid center = default;
        var origin = map.MapCoords;
        await pair.Server.WaitPost(() =>
        {
            var maps = pair.Server.System<SharedMapSystem>();
            var tiles = new List<(Vector2i, Tile)>();
            for (var x = -10; x <= 10; x++)
            for (var y = -10; y <= 10; y++)
                tiles.Add((new Vector2i(x, y), map.Tile.Tile));
            maps.SetTiles(map.Grid.Owner, map.Grid.Comp, tiles);
            var transforms = pair.Server.System<SharedTransformSystem>();
            for (var y = -8; y <= 8; y++)
            {
                var prototype = layout == "directional" ? "WindowDirectional" :
                    layout == "closed_door" && y == 0 ? "Airlock" : "WallSolid";
                var wall = entities.SpawnEntity(prototype, new EntityCoordinates(map.Grid, new Vector2(2.5f, y + 0.5f)));
                if (y == 0)
                    center = wall;
                if (layout == "directional")
                    transforms.SetLocalRotation(wall, Angle.FromDegrees(90));
            }
            if (layout == "corner")
                for (var x = -8; x <= 1; x++)
                    entities.SpawnEntity("WallSolid", new EntityCoordinates(map.Grid, new Vector2(x + 0.5f, 2.5f)));
            if (layout == "rotated_grid")
                transforms.SetLocalRotation(map.Grid, Angle.FromDegrees(37));
            origin = transforms.ToMapCoordinates(new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f)));
            var observer = entities.SpawnEntity("PredictionSpellTestCaster", map.GridCoords);
            var players = pair.Server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
            players.SetAttachedEntity(players.Sessions.Single(), observer);
        });
        await pair.RunTicksSync(8);
        return new Scene(map.Grid, map.MapId, origin, center);
    }

    private static ExplosionVisualsState Preview(TestPair pair, MapCoordinates origin, float total = 100, float max = 1)
        => pair.Server.System<ServerExplosions>().GenerateExplosionPreview(
            new SpawnExplosionEuiMsg.PreviewRequest(origin, "Default", total, 2, max))!;

    private static async Task Compare(TestPair pair, Scene scene, bool crosses, float total = 100, float max = 1)
    {
        var se = pair.Server.ResolveDependency<IEntityManager>();
        var ce = pair.Client.ResolveDependency<IEntityManager>();
        ExplosionVisualsState native = default!;
        await pair.Server.WaitAssertion(() =>
        {
            native = Preview(pair, scene.Origin, total, max);
            Assert.That(native.Tiles[se.GetNetEntity(scene.Grid.Owner)].Values.SelectMany(tiles => tiles).Any(tile => tile.X > 2),
                Is.EqualTo(crosses), "The server scenario must exercise the expected barrier/breakthrough.");
        });
        await pair.Client.WaitAssertion(() => Same(native,
            pair.Client.System<ClientExplosions>().GetPredictedVisuals(scene.Origin, "Default", total, 2, max)!, ce));
    }

    [TestCase("solid")]
    [TestCase("directional")]
    [TestCase("closed_door")]
    [TestCase("corner")]
    [TestCase("rotated_grid")]
    public async Task PredictedFireCannotCrossNativeBlockingWalls(string layout)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var scene = await Barrier(pair, layout);
        await Compare(pair, scene, false);
        if (layout == "corner")
            await pair.Server.WaitAssertion(() =>
                Assert.That(Preview(pair, scene.Origin).Tiles.Values.SelectMany(sets => sets.Values)
                    .SelectMany(tiles => tiles).Any(tile => tile.Y > 2), Is.False));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OpeningAndClosingDoorUpdatesPredictedPropagation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var scene = await Barrier(pair, "closed_door");
        var se = pair.Server.ResolveDependency<IEntityManager>();
        foreach (var open in new[] { false, true, false })
        {
            await pair.Server.WaitPost(() =>
            {
                var doors = pair.Server.System<SharedDoorSystem>();
                if (open)
                    doors.StartOpening(scene.Center);
                else if (se.GetComponent<DoorComponent>(scene.Center).State != DoorState.Closed)
                    doors.StartClosing(scene.Center);
            });
            await pair.RunTicksSync(45);
            await Compare(pair, scene, open);
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WallDamageChangesThePredictedBreakthroughThresholdWithoutApplyingClientDamage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var scene = await Barrier(pair);
        var se = pair.Server.ResolveDependency<IEntityManager>();
        var ce = pair.Client.ResolveDependency<IEntityManager>();
        foreach (var damage in new[] { 0, 180, -180 })
        {
            if (damage != 0)
                await pair.Server.WaitPost(() => pair.Server.System<DamageableSystem>().TryChangeDamage(scene.Center,
                    new DamageSpecifier { DamageDict = new() { ["Blunt"] = damage } }, ignoreResistances: true));
            await pair.RunTicksSync(6);
            var clientWall = ce.GetEntity(se.GetNetEntity(scene.Center));
            var before = ce.GetComponent<DamageableComponent>(clientWall).TotalDamage;
            await Compare(pair, scene, damage == 180, 200, 4);
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(ce.GetComponent<DamageableComponent>(clientWall).TotalDamage, Is.EqualTo(before));
                Assert.That(ce.EntityExists(clientWall), Is.True);
            });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnanchoringReanchoringAndRemovingWallChangesThePredictedBarrier()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var scene = await Barrier(pair);
        for (var stage = 0; stage < 4; stage++)
        {
            await pair.Server.WaitPost(() =>
            {
                var transforms = pair.Server.System<SharedTransformSystem>();
                if (stage == 1)
                    transforms.Unanchor(scene.Center);
                else if (stage == 2)
                    transforms.AnchorEntity(scene.Center);
                else if (stage == 3)
                    pair.Server.ResolveDependency<IEntityManager>().DeleteEntity(scene.Center);
            });
            await pair.RunTicksSync(6);
            await Compare(pair, scene, stage is 1 or 3);
        }
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task GridEdgesAndSpaceTransitionsUseTheSameBlockedFootprint(bool rotateSecondGrid)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var se = pair.Server.ResolveDependency<IEntityManager>();
        var ce = pair.Client.ResolveDependency<IEntityManager>();
        var origin = map.MapCoords;
        await pair.Server.WaitPost(() =>
        {
            var maps = pair.Server.System<SharedMapSystem>();
            var transforms = pair.Server.System<SharedTransformSystem>();
            var first = new List<(Vector2i, Tile)>();
            for (var x = -6; x <= 0; x++)
            for (var y = -6; y <= 6; y++)
                first.Add((new Vector2i(x, y), map.Tile.Tile));
            maps.SetTiles(map.Grid.Owner, map.Grid.Comp, first);
            var second = maps.CreateGridEntity(map.MapId); //spellward
            transforms.SetLocalPosition(second, new Vector2(3, 0));
            if (rotateSecondGrid)
                transforms.SetLocalRotation(second, Angle.FromDegrees(30));
            var tiles = new List<(Vector2i, Tile)>();
            for (var x = 0; x <= 6; x++)
            for (var y = -3; y <= 3; y++)
                tiles.Add((new Vector2i(x, y), map.Tile.Tile));
            maps.SetTiles(second.Owner, second.Comp, tiles);
            for (var y = -3; y <= 3; y++)
                se.SpawnEntity("WallSolid", new EntityCoordinates(second, new Vector2(0.5f, y + 0.5f)));
            origin = transforms.ToMapCoordinates(new EntityCoordinates(map.Grid, new Vector2(-0.5f, 0.5f)));
            var observer = se.SpawnEntity("PredictionSpellTestCaster", new EntityCoordinates(map.Grid, new Vector2(-0.5f, 0.5f)));
            var players = pair.Server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
            players.SetAttachedEntity(players.Sessions.Single(), observer);
        });
        await pair.RunTicksSync(8);
        ExplosionVisualsState native = default!;
        await pair.Server.WaitAssertion(() =>
        {
            native = Preview(pair, origin, 400, 4);
            Assert.That(native.SpaceTiles, Is.Not.Null.And.Not.Empty);
            Assert.That(native.Tiles.Count, Is.EqualTo(2));
        });
        await pair.Client.WaitAssertion(() => Same(native,
            pair.Client.System<ClientExplosions>().GetPredictedVisuals(origin, "Default", 400, 2, 4)!, ce));
        await pair.CleanReturnAsync();
    }

    private static void Same(ExplosionVisualsState native, ExplosionVisualsComponent predicted, IEntityManager entities)
    {
        Assert.That(predicted, Is.Not.Null);
        Assert.That(predicted.Intensity, Is.EqualTo(native.Intensity).Within(0.00001));
        Assert.That(predicted.SpaceTileSize, Is.EqualTo(native.SpaceTileSize));
        Assert.That(predicted.SpaceMatrix, Is.EqualTo(native.SpaceMatrix));
        Assert.That(predicted.Tiles.Keys.Select(grid => entities.GetNetEntity(grid)), Is.EquivalentTo(native.Tiles.Keys));
        foreach (var (grid, tiles) in predicted.Tiles)
            SameTiles(native.Tiles[entities.GetNetEntity(grid)], tiles);
        SameTiles(native.SpaceTiles, predicted.SpaceTiles);
    }
    private static void SameTiles(Dictionary<int, List<Vector2i>>? native, Dictionary<int, List<Vector2i>>? predicted)
    {
        if (native == null)
        {
            Assert.That(predicted, Is.Null);
            return;
        }
        Assert.That(predicted, Is.Not.Null);
        Assert.That(predicted!.Keys, Is.EquivalentTo(native.Keys));
        foreach (var (iteration, tiles) in predicted)
            Assert.That(tiles, Is.EquivalentTo(native[iteration]));
    }
}
// spellward end
