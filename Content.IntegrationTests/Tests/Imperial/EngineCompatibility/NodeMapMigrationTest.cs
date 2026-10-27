using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.NodeContainer;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class NodeMapMigrationTest
{
    // Default groups isolate spatial connectivity from the generator/power simulation.
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ExperimentTegGeneratorNode
  components:
  - type: Transform
    anchored: true
  - type: NodeContainer
    nodes:
      test: !type:TegNodeGenerator {}

- type: entity
  id: ExperimentTegCirculatorNode
  components:
  - type: Transform
    anchored: true
  - type: NodeContainer
    nodes:
      test: !type:TegNodeCirculator {}

- type: entity
  id: ExperimentMyrmexPipeNode
  components:
  - type: Transform
    anchored: true
  - type: MyrmexPipe
    pipeType: Straight
  - type: NodeContainer
    nodes:
      test: !type:MyrmexPipeNode {}

- type: entity
  id: ExperimentMyrmexDeviceNode
  components:
  - type: Transform
    anchored: true
  - type: NodeContainer
    nodes:
      test: !type:MyrmexDeviceNode {}

- type: entity
  parent: ExperimentMyrmexDeviceNode
  id: ExperimentMyrmexEndpointNode
  components:
  - type: MyrmexPipeEndpoint
";

    [TestCase(false)]
    [TestCase(true)]
    public async Task TegConnectionsRespectFacingAndUnanchoring(bool rotateGrid)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            PrepareGrid(entities, map.Grid, map.Tile.Tile, rotateGrid);
            var transforms = server.System<SharedTransformSystem>();
            var generator = Spawn(entities, "ExperimentTegGeneratorNode", map.Grid, new Vector2(-1.5f, -1.5f));
            var right = Spawn(entities, "ExperimentTegCirculatorNode", map.Grid, new Vector2(-0.5f, -1.5f));
            var left = Spawn(entities, "ExperimentTegCirculatorNode", map.Grid, new Vector2(-2.5f, -1.5f));
            transforms.SetLocalRotation(generator, Angle.FromDegrees(90));
            transforms.SetLocalRotation(right, Angle.Zero);
            transforms.SetLocalRotation(left, Angle.FromDegrees(180));

            Assert.That(Reachable(entities, generator, map.Grid), Is.EquivalentTo(new[] { left, right }));
            Assert.That(Reachable(entities, left, map.Grid), Is.EquivalentTo(new[] { generator }));
            Assert.That(Reachable(entities, right, map.Grid), Is.EquivalentTo(new[] { generator }));

            transforms.SetLocalRotation(right, Angle.FromDegrees(90));
            Assert.That(Reachable(entities, generator, map.Grid), Is.EquivalentTo(new[] { left }));
            Assert.That(Reachable(entities, right, map.Grid), Is.Empty);

            transforms.Unanchor(left);
            Assert.That(Reachable(entities, generator, map.Grid), Is.Empty);
            Assert.That(Reachable(entities, left, map.Grid), Is.Empty);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task MyrmexConnectionsRespectPortsEndpointsAndUnanchoring(bool rotateGrid)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            PrepareGrid(entities, map.Grid, map.Tile.Tile, rotateGrid);
            var transforms = server.System<SharedTransformSystem>();
            var pipe = Spawn(entities, "ExperimentMyrmexPipeNode", map.Grid, new Vector2(-1.5f, -1.5f));
            var right = Spawn(entities, "ExperimentMyrmexPipeNode", map.Grid, new Vector2(-0.5f, -1.5f));
            var above = Spawn(entities, "ExperimentMyrmexPipeNode", map.Grid, new Vector2(-1.5f, -0.5f));
            var device = Spawn(entities, "ExperimentMyrmexDeviceNode", map.Grid, new Vector2(-2.5f, -1.5f));
            var endpoint = Spawn(entities, "ExperimentMyrmexEndpointNode", map.Grid, new Vector2(-1.5f, -1.5f));

            transforms.SetLocalRotation(above, Angle.FromDegrees(90));
            transforms.SetLocalRotation(pipe, Angle.FromDegrees(90));
            transforms.SetLocalRotation(right, Angle.FromDegrees(90));
            // The orthogonal straight pipe above has no port toward the center.
            Assert.That(Reachable(entities, pipe, map.Grid), Is.EquivalentTo(new[] { right, device, endpoint }));
            Assert.That(Reachable(entities, device, map.Grid), Is.EquivalentTo(new[] { pipe }));
            Assert.That(Reachable(entities, endpoint, map.Grid), Is.EquivalentTo(new[] { pipe }));

            transforms.SetLocalRotation(right, Angle.FromDegrees(180));
            Assert.That(Reachable(entities, pipe, map.Grid), Is.EquivalentTo(new[] { device, endpoint }));
            transforms.Unanchor(device);
            Assert.That(Reachable(entities, pipe, map.Grid), Is.EquivalentTo(new[] { endpoint }));
            Assert.That(Reachable(entities, device, map.Grid), Is.Empty);
        });

        await pair.CleanReturnAsync();
    }

    private static void PrepareGrid(IEntityManager entities, Entity<MapGridComponent> grid, Tile tile, bool rotate)
    {
        var tiles = new List<(Vector2i, Tile)>();
        for (var x = -4; x <= 1; x++)
        for (var y = -4; y <= 1; y++)
            tiles.Add((new Vector2i(x, y), tile));
        entities.System<SharedMapSystem>().SetTiles(grid, grid.Comp, tiles);
        var transforms = entities.System<SharedTransformSystem>();
        transforms.SetLocalPosition(grid, new Vector2(30, 40));
        if (rotate)
            transforms.SetLocalRotation(grid, Angle.FromDegrees(37));
    }

    private static EntityUid Spawn(IEntityManager entities, string prototype, EntityUid grid, Vector2 position)
    {
        var entity = entities.SpawnEntity(prototype, new EntityCoordinates(grid, position));
        Assert.That(entities.GetComponent<TransformComponent>(entity).Anchored, Is.True);
        return entity;
    }

    private static EntityUid[] Reachable(IEntityManager entities, EntityUid entity, Entity<MapGridComponent> grid)
    {
        var node = entities.GetComponent<NodeContainerComponent>(entity).Nodes["test"];
        return node.GetReachableNodes(entities.GetComponent<TransformComponent>(entity),
                entities.GetEntityQuery<NodeContainerComponent>(), entities.GetEntityQuery<TransformComponent>(), grid.Comp, entities)
            .Select(other => other.Owner).ToArray();
    }
}
