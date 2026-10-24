// spellward start
using System.Linq;
using System.Numerics;
using Content.Client.Projectiles;
using Robust.Client.GameObjects;
using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Projectiles;

[TestFixture]
public sealed class UnguidedProjectilePredictionTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: UnguidedPredictionTestProjectile
  parent: BaseArrow
  components:
  - type: Physics
    bodyType: Dynamic
    bodyStatus: InAir
  - type: Fixtures
    fixtures:
      projectile:
        shape: !type:PhysShapeCircle
          radius: 0.05
        hard: false
        mask: [BulletImpassable]
  - type: Sprite
  - type: Projectile
    deleteOnCollide: false
    damage:
      types:
        Piercing: 20
  - type: EmbeddableProjectile
    sound: null
    offset: 0, 0
- type: entity
  id: UnguidedPredictionTestTarget
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      target:
        shape: !type:PhysShapeCircle
          radius: 0.3
        hard: true
        layer: [BulletImpassable]
  - type: Damageable
    damageContainer: Biological
- type: entity
  id: UnguidedPredictionTestGun
  components:
  - type: Gun
    minAngle: 8
    maxAngle: 8
    defaultSpread: 4
    projectileSpeed: 10
    availableModes: [SemiAuto]
    selectedMode: SemiAuto
    soundGunshot: null
    soundEmpty: null
  - type: BasicEntityAmmoProvider
    proto: UnguidedPredictionTestProjectile
    capacity: 3
""";

    [Test]
    public async Task NativeServerContactStillAppliesDamageAndEmbeddingOnce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        EntityUid target = default;
        EntityUid arrow = default;
        await server.WaitPost(() =>
        {
            target = entities.SpawnEntity("UnguidedPredictionTestTarget", map.GridCoords.Offset(new Vector2(2, 0)));
            var shooter = entities.SpawnEntity(null, map.GridCoords);
            arrow = entities.SpawnEntity("UnguidedPredictionTestProjectile", map.GridCoords);
            server.System<SharedGunSystem>().ShootProjectile(arrow, Vector2.UnitX, Vector2.Zero, shooter, shooter, 10);
        });
        await pair.RunTicksSync(30);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.EqualTo(20));
            Assert.That(entities.GetComponent<ProjectileComponent>(arrow).ProjectileSpent, Is.True);
            Assert.That(entities.GetComponent<EmbeddableProjectileComponent>(arrow).EmbeddedIntoUid, Is.EqualTo(target));
            Assert.That(entities.GetComponent<EmbeddedContainerComponent>(target).EmbeddedObjects, Is.EquivalentTo(new[] { arrow }));
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
            Assert.That(entities.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.EqualTo(20)));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SharedEmbeddingPoseDoesNotCommitDamageOrEmbeddedBookkeeping()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        await server.WaitAssertion(() =>
        {
            var target = entities.SpawnEntity("UnguidedPredictionTestTarget", map.GridCoords);
            var arrow = entities.SpawnEntity("UnguidedPredictionTestProjectile", map.GridCoords.Offset(Vector2.UnitY));
            var embed = entities.GetComponent<EmbeddableProjectileComponent>(arrow);
            var transform = server.System<SharedTransformSystem>();
            var original = transform.GetWorldPosition(arrow);
            server.System<SharedProjectileSystem>().ApplyEmbeddingPose(arrow, target, embed);
            Assert.That(transform.GetWorldPosition(arrow), Is.EqualTo(original));
            Assert.That(entities.GetComponent<TransformComponent>(arrow).ParentUid, Is.EqualTo(target));
            Assert.That(entities.GetComponent<PhysicsComponent>(arrow).BodyType, Is.EqualTo(BodyType.Static));
            Assert.That(entities.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.Zero);
            Assert.That(entities.HasComponent<EmbeddedContainerComponent>(target), Is.False);
            Assert.That(embed.EmbeddedIntoUid, Is.Null);
            entities.DeleteEntity(arrow);
            entities.DeleteEntity(target);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LocalContactEmbedsPresentationWithoutServerOrClientDamage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var serverEntities = server.ResolveDependency<IEntityManager>();
        var clientEntities = client.ResolveDependency<IEntityManager>();
        EntityUid target = default;
        EntityUid player = default;
        EntityUid localArrow = default;
        await server.WaitPost(() =>
        {
            player = serverEntities.SpawnEntity(null, map.GridCoords);
            target = serverEntities.SpawnEntity("UnguidedPredictionTestTarget", map.GridCoords.Offset(new Vector2(2, 0)));
            var players = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
            players.SetAttachedEntity(players.Sessions.Single(), player);
        });
        await pair.RunTicksSync(5);
        var clientPlayer = client.Session!.AttachedEntity!.Value;
        var clientTarget = clientEntities.GetEntity(serverEntities.GetNetEntity(target));
        await client.WaitPost(() =>
        {
            var origin = clientEntities.GetComponent<TransformComponent>(clientPlayer).Coordinates;
            localArrow = clientEntities.SpawnEntity("UnguidedPredictionTestProjectile", origin);
            Assert.That(client.System<ProjectilePredictionSystem>().IsCompatible(localArrow), Is.True);
            clientEntities.AddComponent(localArrow, new ProjectilePredictionComponent
            {
                Shooter = clientPlayer, Weapon = clientEntities.GetNetEntity(clientPlayer),
                InputTick = new GameTick(1), LaunchTick = new GameTick(1),
            });
            client.System<SharedGunSystem>().ShootProjectile(localArrow, Vector2.UnitX, Vector2.Zero, clientPlayer, clientPlayer, 10);
            client.System<SharedPhysicsSystem>().UpdateIsPredicted(localArrow);
        });
        await pair.RunTicksSync(30);
        await client.WaitAssertion(() =>
        {
            Assert.That(clientEntities.GetComponent<ProjectilePredictionComponent>(localArrow).Impact, Is.True);
            Assert.That(clientEntities.GetComponent<TransformComponent>(localArrow).ParentUid, Is.EqualTo(clientTarget));
            Assert.That(clientEntities.GetComponent<DamageableComponent>(clientTarget).TotalDamage.Float(), Is.Zero);
            Assert.That(clientEntities.HasComponent<EmbeddedContainerComponent>(clientTarget), Is.False);
        });
        await server.WaitAssertion(() =>
            Assert.That(serverEntities.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.Zero));
        await client.WaitPost(() => clientEntities.DeleteEntity(localArrow));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AuthoritativeFlightDoesNotReplaceLocalFlightAndTerminalStateRevealsServerEntity()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var serverEntities = server.ResolveDependency<IEntityManager>();
        var clientEntities = client.ResolveDependency<IEntityManager>();
        EntityUid player = default;
        EntityUid serverArrow = default;
        EntityUid localArrow = default;
        await server.WaitPost(() =>
        {
            player = serverEntities.SpawnEntity(null, map.GridCoords);
            serverArrow = serverEntities.SpawnEntity("UnguidedPredictionTestProjectile", map.GridCoords);
            server.System<SharedPhysicsSystem>().SetCanCollide(serverArrow, false);
            var players = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
            players.SetAttachedEntity(players.Sessions.Single(), player);
        });
        await pair.RunTicksSync(5);
        var clientPlayer = client.Session!.AttachedEntity!.Value;
        await client.WaitPost(() =>
        {
            var origin = clientEntities.GetComponent<TransformComponent>(clientPlayer).Coordinates;
            localArrow = clientEntities.SpawnEntity("UnguidedPredictionTestProjectile", origin);
            Assert.That(client.System<ProjectilePredictionSystem>().IsCompatible(localArrow), Is.True);
            clientEntities.AddComponent(localArrow, new ProjectilePredictionComponent
            {
                Shooter = clientPlayer, Weapon = clientEntities.GetNetEntity(clientPlayer),
                InputTick = new GameTick(1), LaunchTick = new GameTick(1),
            });
            client.System<SharedGunSystem>().ShootProjectile(localArrow, Vector2.UnitX, Vector2.Zero, clientPlayer, clientPlayer, 10);
        });
        await server.WaitPost(() =>
        {
            serverEntities.AddComponent(serverArrow, new ProjectilePredictionComponent
            {
                Shooter = player, Weapon = serverEntities.GetNetEntity(player),
                InputTick = new GameTick(1), LaunchTick = new GameTick(1),
            });
            // Deliberately diverge. Routine server flight states must not steer the local projectile.
            server.System<SharedGunSystem>().ShootProjectile(serverArrow, Vector2.UnitY, Vector2.Zero, player, player, 10);
            server.System<SharedPhysicsSystem>().SetCanCollide(serverArrow, true);
        });
        await pair.RunTicksSync(10);
        var clientServerArrow = clientEntities.GetEntity(serverEntities.GetNetEntity(serverArrow));
        await client.WaitAssertion(() =>
        {
            var transform = client.System<SharedTransformSystem>();
            var localPosition = transform.GetWorldPosition(localArrow);
            var serverPosition = transform.GetWorldPosition(clientServerArrow);
            Assert.That(localPosition.X, Is.GreaterThan(serverPosition.X + 0.5f));
            Assert.That(localPosition.Y, Is.LessThan(serverPosition.Y - 0.5f));
            Assert.That(clientEntities.GetComponent<SpriteComponent>(clientServerArrow).Visible, Is.False);
        });
        await server.WaitPost(() =>
        {
            var projectile = serverEntities.GetComponent<ProjectileComponent>(serverArrow);
            projectile.ProjectileSpent = true;
            serverEntities.Dirty(serverArrow, projectile);
        });
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
        {
            Assert.That(clientEntities.EntityExists(localArrow), Is.False);
            Assert.That(clientEntities.GetComponent<SpriteComponent>(clientServerArrow).Visible, Is.True);
        });
        await pair.CleanReturnAsync();
    }


    [Test]
    public async Task OrdinaryFiringPipelinePredictsImmediatelyWithMatchingLaunchAndFireModeState()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var serverEntities = server.ResolveDependency<IEntityManager>();
        var clientEntities = client.ResolveDependency<IEntityManager>();
        EntityUid player = default;
        EntityUid gun = default;
        EntityUid target = default;
        Vector2 clientVelocity = default;
        Vector2 serverVelocity = default;
        var tick = new GameTick(42);
        await server.WaitPost(() =>
        {
            player = serverEntities.SpawnEntity(null, map.GridCoords);
            gun = serverEntities.SpawnEntity("UnguidedPredictionTestGun", map.GridCoords);
            target = serverEntities.SpawnEntity("UnguidedPredictionTestTarget", map.GridCoords.Offset(new Vector2(2, 0)));
            var players = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
            players.SetAttachedEntity(players.Sessions.Single(), player);
        });
        await pair.RunTicksSync(5);
        var clientPlayer = client.Session!.AttachedEntity!.Value;
        var clientGun = clientEntities.GetEntity(serverEntities.GetNetEntity(gun));
        var clientTarget = clientEntities.GetEntity(serverEntities.GetNetEntity(target));
        await client.WaitAssertion(() =>
        {
            var comp = clientEntities.GetComponent<GunComponent>(clientGun);
#pragma warning disable RA0002 // Set the same controlled precondition as OnShootRequest.
            comp.Target = clientTarget;
#pragma warning restore RA0002
            Assert.That(client.System<SharedGunSystem>().AttemptPredictedShoot(clientPlayer, clientGun, comp,
                new EntityCoordinates(clientPlayer, new Vector2(10, 0)), tick, resetShotCounter: false), Is.True);
            var projectiles = clientEntities.EntityQueryEnumerator<ProjectilePredictionComponent, PhysicsComponent>();
            var count = 0;
            while (projectiles.MoveNext(out var uid, out var prediction, out var body))
            {
                if (prediction.InputTick != tick)
                    continue;
                count++;
                clientVelocity = body.LinearVelocity;
                Assert.That(prediction.Weapon, Is.EqualTo(serverEntities.GetNetEntity(gun)));
                Assert.That(clientEntities.GetComponent<TargetedProjectileComponent>(uid).Target, Is.EqualTo(clientTarget));
            }
            // This assertion runs before the server has fired anything.
            Assert.That(count, Is.EqualTo(1));
            Assert.That(clientVelocity.Length(), Is.EqualTo(10).Within(0.001));
            Assert.That(comp.ShotCounter, Is.EqualTo(1));
            Assert.That(comp.ProjectilePredictionSequence, Is.EqualTo(1));
            Assert.That(comp.Target, Is.EqualTo(clientTarget));
        });
        await server.WaitAssertion(() =>
        {
            var comp = serverEntities.GetComponent<GunComponent>(gun);
#pragma warning disable RA0002 // Set the same controlled precondition as OnShootRequest.
            comp.Target = target;
#pragma warning restore RA0002
            Assert.That(server.System<SharedGunSystem>().AttemptPredictedShoot(player, gun, comp,
                new EntityCoordinates(player, new Vector2(10, 0)), tick, resetShotCounter: false), Is.True);
            var projectiles = serverEntities.EntityQueryEnumerator<ProjectilePredictionComponent, PhysicsComponent>();
            while (projectiles.MoveNext(out _, out var prediction, out var body))
                if (prediction.InputTick == tick)
                    serverVelocity = body.LinearVelocity;
            Assert.That(serverVelocity.X, Is.EqualTo(clientVelocity.X).Within(0.001));
            Assert.That(serverVelocity.Y, Is.EqualTo(clientVelocity.Y).Within(0.001));
            Assert.That(comp.ShotCounter, Is.EqualTo(1));
            Assert.That(comp.ProjectilePredictionSequence, Is.EqualTo(1));
            Assert.That(comp.Target, Is.EqualTo(target));
        });
        await pair.RunTicksSync(30);
        await server.WaitAssertion(() =>
            Assert.That(serverEntities.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.EqualTo(20)));
        await pair.CleanReturnAsync();
    }

}
// spellward end
