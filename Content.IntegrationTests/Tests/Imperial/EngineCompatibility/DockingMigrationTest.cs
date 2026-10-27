using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class DockingMigrationTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task DockMoveRebuildsJointWhilePureRotationKeepsIt(bool rotationOnly)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var maps = server.System<SharedMapSystem>();
            var transforms = server.System<SharedTransformSystem>();
            var docks = server.System<DockingSystem>();
            var otherGrid = maps.CreateGridEntity(map.MapId);
            maps.SetTile(otherGrid, Vector2i.Zero, map.Tile.Tile);
            transforms.SetLocalPosition(otherGrid, new Vector2(50, 0));

            var first = entities.SpawnEntity("AirlockShuttle", map.GridCoords);
            var second = entities.SpawnEntity("AirlockShuttle", new EntityCoordinates(otherGrid, Vector2.One / 2));
            var firstDock = entities.GetComponent<DockingComponent>(first);
            var secondDock = entities.GetComponent<DockingComponent>(second);
            docks.Dock((first, firstDock), (second, secondDock));
            var originalJoint = firstDock.DockJoint;
            Assert.That(originalJoint, Is.Not.Null);

            // Stay on the same tile: its anchored-entity lookup does not need updating.
            // This exercises the MoveEvent path used when an anchored port moves without being unanchored.
            var xform = entities.GetComponent<TransformComponent>(first);
            if (rotationOnly)
                transforms.SetLocalRotation(first, Angle.FromDegrees(90));
            else
                transforms.SetCoordinates((first, xform, entities.GetComponent<MetaDataComponent>(first)),
                    xform.Coordinates.Offset(new Vector2(0.1f, 0)), unanchor: false);

            Assert.Multiple(() =>
            {
                Assert.That(xform.Anchored, Is.True);
                Assert.That(firstDock.DockedWith, Is.EqualTo(second));
                Assert.That(secondDock.DockedWith, Is.EqualTo(first));
                Assert.That(firstDock.DockJoint, Is.Not.Null);
                if (rotationOnly)
                    Assert.That(firstDock.DockJoint, Is.SameAs(originalJoint));
                else
                    Assert.That(firstDock.DockJoint, Is.Not.SameAs(originalJoint));
                Assert.That(secondDock.DockJoint, Is.SameAs(firstDock.DockJoint));
            });

            transforms.Unanchor(first);
            Assert.Multiple(() =>
            {
                Assert.That(firstDock.DockedWith, Is.Null);
                Assert.That(secondDock.DockedWith, Is.Null);
                Assert.That(firstDock.DockJoint, Is.Null);
                Assert.That(secondDock.DockJoint, Is.Null);
            });
        });

        await pair.CleanReturnAsync();
    }
}
