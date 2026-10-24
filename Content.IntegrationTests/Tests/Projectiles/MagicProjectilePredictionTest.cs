// spellward start
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Projectiles;
using Content.Shared.Actions;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.TargetOverlay.Events;
using Content.Shared.Imperial.MouseInput.Events;
using Content.Shared.Damage;
using Robust.Client.GameObjects;
using Content.Shared.Projectiles;
using Content.Shared.Trigger.Components.Triggers;
using Robust.Client.Timing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Projectiles;

[TestFixture, NonParallelizable]
public sealed class MagicProjectilePredictionTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: PredictionSpellTestCaster
  components:
  - type: Actions
  - type: DoAfter
  - type: MovementSpeedModifier
  - type: Hands
    hands:
      right:
        location: Right
      left:
        location: Left
    sortedHands: [right, left]
  - type: Physics
    bodyType: Dynamic
    bodyStatus: InAir
  - type: Fixtures
    fixtures:
      body:
        shape: !type:PhysShapeCircle
          radius: 0.1
        hard: true
        density: 1
- type: entity
  id: PredictionSpellTestTarget
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
  id: PredictionSpellTestAction
  components:
  - type: Action
    sound: null
    useDelay: 1
  - type: TargetAction
    range: 100
    checkCanAccess: false
  - type: WorldTargetAction
    event: !type:MedievalProjectileSpellEvent
      projectilePrototype:
        0: MedievalProjectileMagicArrowBeginner
      projectileSpeed: 20
- type: entity
  id: PredictionSpellTestDelayedAction
  parent: PredictionSpellTestAction
  components:
  - type: WorldTargetAction
    event: !type:MedievalProjectileSpellEvent
      projectilePrototype:
        0: MedievalProjectileMagicArrowBeginner
      projectileSpeed: 20
      spellCastDoAfter:
        delay: 0.2
- type: entity
  id: PredictionSpellTestTargetAction
  parent: PredictionSpellTestAction
  components:
  - type: WorldTargetAction
    event: !type:MedievalEntityTargetProjectileSpellEvent
      projectilePrototype: MedievalProjectileMagicArrowBeginner
      projectileSpeed: 20
- type: entity
  id: PredictionSpellTestCurrentTargetAction
  parent: PredictionSpellTestAction
  components:
  - type: MedievalTargetSpell
  - type: WorldTargetAction
    event: !type:MedievalProjectileSpellEvent
      projectilePrototype:
        0: MedievalProjectileMagicArrowBeginner
      targetSpellType: TargetOnCurrentMousePosition
- type: entity
  id: PredictionSpellTestGuidedAction
  parent: PredictionSpellTestAction
  components:
  - type: WorldTargetAction
    event: !type:MedievalProjectileSpellEvent
      projectilePrototype:
        0: MedievalProjectileFireBoltTier2
- type: entity
  id: PredictionSpellTestStockAction
  parent: PredictionSpellTestAction
  components:
  - type: WorldTargetAction
    event: !type:ProjectileSpellEvent
      prototype: MedievalProjectileMagicArrowBeginner
""";

    [TestCase("PredictionSpellTestAction", false)]
    [TestCase("PredictionSpellTestDelayedAction", true)]
    [TestCase("PredictionSpellTestStockAction", false)]
    public async Task RealActionRequestLaunchesMatchingSpellWithoutReplayDuplicates(string prototype, bool delayed)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var se = server.ResolveDependency<IEntityManager>();
        var ce = client.ResolveDependency<IEntityManager>();
        EntityUid caster = default;
        EntityUid action = default;
        await server.WaitPost(() =>
        {
            caster = se.SpawnEntity("PredictionSpellTestCaster", map.GridCoords);
            action = server.System<SharedActionsSystem>().AddAction(caster, prototype)!.Value;
            var players = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
            players.SetAttachedEntity(players.Sessions.Single(), caster);
        });
        await pair.RunTicksSync(5);
        var netAction = se.GetNetEntity(action);
        GameTick inputTick = default;
        await client.WaitAssertion(() =>
        {
            inputTick = client.ResolveDependency<IClientGameTiming>().CurTick;
            var target = ce.GetComponent<TransformComponent>(client.Session!.AttachedEntity!.Value).Coordinates.Offset(new Vector2(30, 0));
            ce.RaisePredictiveEvent(new RequestPerformActionEvent(netAction, null, ce.GetNetCoordinates(target))
            {
                InputTick = inputTick,
            });
            Assert.That(Shots(ce, netAction, inputTick, true).Count, Is.EqualTo(delayed ? 0 : 1));
        });
        // The normal network request, cast timer, and prediction replays all run here.
        await pair.RunTicksSync(20);
        EntityUid local = default;
        EntityUid authoritative = default;
        await client.WaitAssertion(() =>
        {
            var shots = Shots(ce, netAction, inputTick, true);
            Assert.That(shots.Count, Is.EqualTo(1));
            local = shots.Single();
            Assert.That(ce.HasComponent<TriggerOnCollideComponent>(local), Is.False);
            Assert.That(ce.GetComponent<PhysicsComponent>(local).LinearVelocity.X, Is.InRange(15f, 20.001f));
            Assert.That(ce.GetComponent<PhysicsComponent>(local).LinearVelocity.Y, Is.Zero.Within(0.001));
            Assert.That(ce.GetComponent<ProjectileComponent>(local).Weapon, Is.EqualTo(client.Session!.AttachedEntity));
        });
        await server.WaitAssertion(() =>
        {
            var shots = Shots(se, netAction, inputTick, false);
            Assert.That(shots.Count, Is.EqualTo(1));
            authoritative = shots.Single();
            Assert.That(se.HasComponent<TriggerOnCollideComponent>(authoritative), Is.True);
            Assert.That(se.GetComponent<PhysicsComponent>(authoritative).LinearVelocity.X, Is.InRange(15f, 20.001f));
        });
        var serverOnClient = ce.GetEntity(se.GetNetEntity(authoritative));
        await client.WaitAssertion(() =>
            Assert.That(ce.GetComponent<Robust.Client.GameObjects.SpriteComponent>(serverOnClient).Visible, Is.False));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TargetSelectionPredictsEachProjectileUsingTheOriginalCastIdentity()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var se = server.ResolveDependency<IEntityManager>();
        var ce = client.ResolveDependency<IEntityManager>();
        EntityUid caster = default;
        EntityUid action = default;
        await server.WaitPost(() =>
        {
            caster = se.SpawnEntity("PredictionSpellTestCaster", map.GridCoords);
            action = server.System<SharedActionsSystem>().AddAction(caster, "PredictionSpellTestTargetAction")!.Value;
            var players = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
            players.SetAttachedEntity(players.Sessions.Single(), caster);
        });
        await pair.RunTicksSync(5);
        var netAction = se.GetNetEntity(action);
        GameTick inputTick = default;
        await client.WaitPost(() =>
        {
            inputTick = client.ResolveDependency<IClientGameTiming>().CurTick;
            var target = ce.GetComponent<TransformComponent>(client.Session!.AttachedEntity!.Value).Coordinates.Offset(new Vector2(30, 0));
            ce.RaisePredictiveEvent(new RequestPerformActionEvent(netAction, null, ce.GetNetCoordinates(target)) { InputTick = inputTick });
            Assert.That(Shots(ce, netAction, inputTick, true), Is.Empty);
        });
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
        {
            var player = client.Session!.AttachedEntity!.Value;
            var origin = client.System<SharedTransformSystem>().GetMapCoordinates(player);
            ce.RaisePredictiveEvent(new TargetOverlayShootEvent
            {
                Performer = ce.GetNetEntity(player), Sender = netAction,
                Targets = new()
                {
                    (new MapCoordinates(origin.Position + new Vector2(30, 0), origin.MapId), null),
                    (new MapCoordinates(origin.Position + new Vector2(0, 30), origin.MapId), null),
                },
            });
            Assert.That(Shots(ce, netAction, inputTick, true).Count, Is.EqualTo(2));
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            var shots = Shots(se, netAction, inputTick, false);
            Assert.That(shots.Count, Is.EqualTo(2));
            Assert.That(shots.Select(uid => se.GetComponent<ProjectilePredictionComponent>(uid).Index), Is.EquivalentTo(new[] { 0, 1 }));
            var velocities = shots.Select(uid => se.GetComponent<PhysicsComponent>(uid).LinearVelocity).ToArray();
            Assert.That(velocities.Any(v => v.X > 15f && MathF.Abs(v.Y) < 0.001f), Is.True);
            Assert.That(velocities.Any(v => v.Y > 15f && MathF.Abs(v.X) < 0.001f), Is.True);
        });
        await client.WaitAssertion(() => Assert.That(Shots(ce, netAction, inputTick, true).Count, Is.EqualTo(2)));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CompletionAimReusesThePredictedCursorSample()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var se = server.ResolveDependency<IEntityManager>();
        var ce = client.ResolveDependency<IEntityManager>();
        EntityUid caster = default;
        EntityUid action = default;
        await server.WaitPost(() =>
        {
            caster = se.SpawnEntity("PredictionSpellTestCaster", map.GridCoords);
            action = server.System<SharedActionsSystem>().AddAction(caster, "PredictionSpellTestCurrentTargetAction")!.Value;
            var players = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
            players.SetAttachedEntity(players.Sessions.Single(), caster);
        });
        await pair.RunTicksSync(5);
        var netAction = se.GetNetEntity(action);
        GameTick tick = default;
        await client.WaitAssertion(() =>
        {
            var player = client.Session!.AttachedEntity!.Value;
            tick = client.ResolveDependency<IClientGameTiming>().CurTick;
            var target = ce.GetComponent<TransformComponent>(player).Coordinates.Offset(new Vector2(30, 0));
            ce.RaisePredictiveEvent(new RequestPerformActionEvent(netAction, null, ce.GetNetCoordinates(target)) { InputTick = tick });
            var clientAction = ce.GetEntity(netAction);
            // Headless clients have no real mouse; provide the local completion sample directly.
            ce.EventBus.RaiseLocalEvent(clientAction, new MedievalSpellRelayEvent<MousePositionRefreshEvent>(
                new MousePositionRefreshEvent { Player = player, MousePosition = target }));
            Assert.That(Shots(ce, netAction, tick, true).Count, Is.EqualTo(1));
            // A later cursor position must not replace the sample used for this flight.
            ce.EventBus.RaiseLocalEvent(clientAction, new MedievalSpellRelayEvent<MousePositionRefreshEvent>(
                new MousePositionRefreshEvent { Player = player, MousePosition = target.Offset(new Vector2(-30, 30)) }));
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            var uid = Shots(se, netAction, tick, false).Single();
            var body = se.GetComponent<PhysicsComponent>(uid);
            Assert.That(body.LinearVelocity.X, Is.GreaterThan(15));
            Assert.That(body.LinearVelocity.Y, Is.Zero.Within(0.001));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpriteSmoothingKeepsThePhysicsPoseAndFlightHeadingUnchanged()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var se = server.ResolveDependency<IEntityManager>();
        var ce = client.ResolveDependency<IEntityManager>();
        EntityUid caster = default;
        await server.WaitPost(() =>
        {
            caster = se.SpawnEntity("PredictionSpellTestCaster", map.GridCoords);
            var players = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
            players.SetAttachedEntity(players.Sessions.Single(), caster);
        });
        await pair.RunTicksSync(5);
        var key = new ProjectilePredictionKey(se.GetNetEntity(caster), new GameTick(99), 0);
        await client.WaitPost(() =>
        {
            var player = client.Session!.AttachedEntity!.Value;
            client.System<ProjectilePredictionSystem>().TryLaunchSpell("MedievalProjectileMagicArrowBeginner", player, key,
                ce.GetComponent<TransformComponent>(player).Coordinates, new Vector2(1, 1), 20);
        });
        await pair.RunTicksSync(3);
        await client.WaitAssertion(() =>
        {
            var uid = Shots(ce, key.Weapon, key.InputTick, true).Single();
            var transform = client.System<TransformSystem>();
            var prediction = client.System<ProjectilePredictionSystem>();
            var timing = client.ResolveDependency<IClientGameTiming>();
            var position = transform.GetWorldPosition(uid);
            var heading = transform.GetWorldRotation(uid);
            var sprite = ce.GetComponent<SpriteComponent>(uid);
            sprite.NoRotation = false;
            var remainder = timing.TickRemainder;
            try
            {
                timing.TickRemainder = TimeSpan.Zero;
                prediction.FrameUpdate(0);
                transform.FrameUpdate(0);
                var first = position + heading.RotateVec(sprite.Offset);
                timing.TickRemainder = timing.TickPeriod / 2;
                prediction.FrameUpdate(0);
                transform.FrameUpdate(0);
                var second = position + heading.RotateVec(sprite.Offset);
                Assert.That(transform.GetWorldPosition(uid), Is.EqualTo(position));
                Assert.That(transform.GetWorldRotation(uid), Is.EqualTo(heading));
                Assert.That(second.X, Is.GreaterThan(first.X));
                Assert.That(second.Y, Is.GreaterThan(first.Y));
                Assert.That(second.X - first.X, Is.EqualTo(second.Y - first.Y).Within(0.001));
            }
            finally { timing.TickRemainder = remainder; }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LocalTriggeredSpellContactDoesNotApplyClientOrServerDamage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var se = server.ResolveDependency<IEntityManager>();
        var ce = client.ResolveDependency<IEntityManager>();
        EntityUid caster = default;
        EntityUid target = default;
        await server.WaitPost(() =>
        {
            caster = se.SpawnEntity("PredictionSpellTestCaster", map.GridCoords);
            target = se.SpawnEntity("PredictionSpellTestTarget", map.GridCoords.Offset(new Vector2(2, 0)));
            var players = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
            players.SetAttachedEntity(players.Sessions.Single(), caster);
        });
        await pair.RunTicksSync(5);
        var key = new ProjectilePredictionKey(se.GetNetEntity(caster), new GameTick(101), 0);
        await client.WaitPost(() =>
        {
            var player = client.Session!.AttachedEntity!.Value;
            Assert.That(client.System<ProjectilePredictionSystem>().TryLaunchSpell("MedievalProjectileMagicArrowBeginner", player, key,
                ce.GetComponent<TransformComponent>(player).Coordinates, Vector2.UnitX, 20), Is.True);
        });
        await pair.RunTicksSync(15);
        var clientTarget = ce.GetEntity(se.GetNetEntity(target));
        await client.WaitAssertion(() =>
        {
            var uid = Shots(ce, key.Weapon, key.InputTick, true).Single();
            Assert.That(ce.GetComponent<ProjectilePredictionComponent>(uid).Impact, Is.True);
            Assert.That(ce.GetComponent<SpriteComponent>(uid).Visible, Is.False);
            Assert.That(ce.GetComponent<DamageableComponent>(clientTarget).TotalDamage.Float(), Is.Zero);
        });
        await server.WaitAssertion(() => Assert.That(se.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.Zero));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GuidedSpellKeepsItsOrdinaryServerPresentation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var se = server.ResolveDependency<IEntityManager>();
        var ce = client.ResolveDependency<IEntityManager>();
        EntityUid caster = default;
        EntityUid action = default;
        await server.WaitPost(() =>
        {
            caster = se.SpawnEntity("PredictionSpellTestCaster", map.GridCoords);
            action = server.System<SharedActionsSystem>().AddAction(caster, "PredictionSpellTestGuidedAction")!.Value;
            var players = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
            players.SetAttachedEntity(players.Sessions.Single(), caster);
        });
        await pair.RunTicksSync(5);
        var netAction = se.GetNetEntity(action);
        GameTick tick = default;
        await client.WaitAssertion(() =>
        {
            tick = client.ResolveDependency<IClientGameTiming>().CurTick;
            var target = ce.GetComponent<TransformComponent>(client.Session!.AttachedEntity!.Value).Coordinates.Offset(new Vector2(30, 0));
            ce.RaisePredictiveEvent(new RequestPerformActionEvent(netAction, null, ce.GetNetCoordinates(target)) { InputTick = tick });
            Assert.That(Shots(ce, netAction, tick, true), Is.Empty);
        });
        await pair.RunTicksSync(10);
        EntityUid authoritative = default;
        await server.WaitAssertion(() => authoritative = Shots(se, netAction, tick, false).Single());
        var serverOnClient = ce.GetEntity(se.GetNetEntity(authoritative));
        await client.WaitAssertion(() =>
        {
            Assert.That(Shots(ce, netAction, tick, true), Is.Empty);
            Assert.That(ce.GetComponent<SpriteComponent>(serverOnClient).Visible, Is.True);
        });
        await pair.CleanReturnAsync();
    }

    private static List<EntityUid> Shots(IEntityManager entities, NetEntity action, GameTick tick, bool clientSide)
    {
        var result = new List<EntityUid>();
        var query = entities.EntityQueryEnumerator<ProjectilePredictionComponent>();
        while (query.MoveNext(out var uid, out var marker))
            if (marker.Weapon == action && marker.InputTick == tick && entities.IsClientSide(uid) == clientSide)
                result.Add(uid);
        return result;
    }
}
// spellward end
