// spellward start
using System.Linq;
using Content.Client.Projectiles;
using Content.Shared.Projectiles;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Projectiles;

[TestFixture]
public sealed class ProjectilePredictionRegistryTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: ProjectilePredictionRegistryTestArrow
  parent: BaseArrow
  components:
  - type: Projectile
    deleteOnCollide: false
    damage:
      types:
        Piercing: 1
""";

    [TestCase(false)]
    [TestCase(true)]
    public async Task ReusedArrowRetiresOldPredictionAndCanBeRegisteredAgain(bool changesShooter)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var se = server.ResolveDependency<IEntityManager>();
        var ce = client.ResolveDependency<IEntityManager>();
        EntityUid player = default;
        EntityUid other = default;
        EntityUid arrow = default;
        await server.WaitPost(() =>
        {
            player = se.SpawnEntity(null, map.GridCoords);
            other = se.SpawnEntity(null, map.GridCoords);
            arrow = se.SpawnEntity("ProjectilePredictionRegistryTestArrow", map.GridCoords);
            var projectile = se.GetComponent<ProjectileComponent>(arrow);
            projectile.Shooter = player;
            se.Dirty(arrow, projectile);
            server.System<SharedPhysicsSystem>().SetCanCollide(arrow, false);
            var players = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
            players.SetAttachedEntity(players.Sessions.Single(), player);
        });
        await pair.RunTicksSync(8);
        var clientPlayer = client.Session!.AttachedEntity!.Value;
        var firstTick = new GameTick(401);
        var nextTick = new GameTick(402);
        EntityUid predicted = default;

        await client.WaitPost(() => predicted = LocalArrow(firstTick));
        await server.WaitPost(() => se.AddComponent(arrow, new ProjectilePredictionComponent
        {
            Shooter = player, Weapon = se.GetNetEntity(player), InputTick = firstTick, LaunchTick = firstTick,
        }));
        await pair.RunTicksSync(6);
        var authoritative = ce.GetEntity(se.GetNetEntity(arrow));
        await client.WaitAssertion(() =>
        {
            Assert.That(ce.EntityExists(predicted), Is.True);
            Assert.That(ce.GetComponent<SpriteComponent>(authoritative).Visible, Is.False);
        });

        await server.WaitPost(() =>
        {
            var marker = se.GetComponent<ProjectilePredictionComponent>(arrow);
            marker.Shooter = changesShooter ? other : player;
            marker.InputTick = changesShooter ? firstTick : nextTick;
            se.Dirty(arrow, marker);
            var projectile = se.GetComponent<ProjectileComponent>(arrow);
            projectile.Shooter = marker.Shooter;
            se.Dirty(arrow, projectile);
        });
        await pair.RunTicksSync(6);
        await client.WaitAssertion(() =>
        {
            Assert.That(ce.EntityExists(predicted), Is.False, "The old shot must retire when an arrow is reused.");
            Assert.That(ce.GetComponent<SpriteComponent>(authoritative).Visible, Is.True,
                "An arrow fired by somebody else must not remain hidden.");
        });

        await client.WaitPost(() => predicted = LocalArrow(nextTick));
        await server.WaitPost(() =>
        {
            var marker = se.GetComponent<ProjectilePredictionComponent>(arrow);
            marker.Shooter = player;
            marker.InputTick = nextTick;
            marker.LaunchTick = nextTick;
            se.Dirty(arrow, marker);
            var projectile = se.GetComponent<ProjectileComponent>(arrow);
            projectile.Shooter = player;
            se.Dirty(arrow, projectile);
        });
        await pair.RunTicksSync(6);
        await client.WaitAssertion(() =>
            Assert.That(ce.GetComponent<SpriteComponent>(authoritative).Visible, Is.False));

        await server.WaitPost(() => se.RemoveComponent<ProjectilePredictionComponent>(arrow));
        await pair.RunTicksSync(6);
        await client.WaitAssertion(() =>
        {
            Assert.That(ce.EntityExists(predicted), Is.False);
            Assert.That(ce.GetComponent<SpriteComponent>(authoritative).Visible, Is.True);
        });
        await pair.CleanReturnAsync();

        EntityUid LocalArrow(GameTick tick)
        {
            var uid = ce.SpawnEntity("ProjectilePredictionRegistryTestArrow",
                ce.GetComponent<TransformComponent>(clientPlayer).Coordinates);
            client.System<SharedPhysicsSystem>().SetCanCollide(uid, false);
            ce.AddComponent(uid, new ProjectilePredictionComponent
            {
                Shooter = clientPlayer, Weapon = ce.GetNetEntity(clientPlayer), InputTick = tick, LaunchTick = tick,
            });
            return uid;
        }
    }
}
// spellward end
