// spellward start
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Explosion;
using Content.Client.Projectiles;
using Content.Shared.Administration;
using Content.Shared.Damage;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.Imperial.ShockWave;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using ServerExplosions = Content.Server.Explosion.EntitySystems.ExplosionSystem;
using ClientExplosions = Content.Client.Explosion.EntitySystems.ExplosionSystem;

namespace Content.IntegrationTests.Tests.Projectiles;

[TestFixture, NonParallelizable]
public sealed class ProjectileExplosionPredictionTest
{
    [TestCase(1f, 2f, 1f)]
    [TestCase(36f, 8f, 2f)]
    [TestCase(90f, 6f, 5f)]
    [TestCase(200f, 6f, 40f)]
    public async Task OpenGroundForecastMatchesNativeExplosionTiles(float total, float slope, float max)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        ExplosionVisualsState native = default!;
        var origin = new MapCoordinates(new Vector2(1000.5f, 1000.5f), map.MapId);
        await pair.Server.WaitAssertion(() => native = pair.Server.System<ServerExplosions>().GenerateExplosionPreview(
            new SpawnExplosionEuiMsg.PreviewRequest(origin, "Default", total, slope, max))!);
        await pair.Client.WaitAssertion(() =>
        {
            var forecast = pair.Client.System<ClientExplosions>().GetPredictedVisuals(origin, "Default", total, slope, max)!;
            Assert.That(forecast, Is.Not.Null);
            Assert.That(forecast.Intensity, Is.EqualTo(native.Intensity).Within(0.00001));
            Assert.That(forecast.SpaceTiles!.Keys, Is.EquivalentTo(native.SpaceTiles!.Keys));
            foreach (var (iteration, tiles) in forecast.SpaceTiles)
                Assert.That(tiles, Is.EquivalentTo(native.SpaceTiles[iteration]));
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task FireballFeedbackIsImmediateAndAuthoritativeConfirmationDoesNotRepeatIt(bool predict)
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
        var key = new ProjectilePredictionKey(se.GetNetEntity(caster), new GameTick(201), 0);
        if (predict)
        {
            await client.WaitAssertion(() =>
            {
                var player = client.Session!.AttachedEntity!.Value;
                Assert.That(client.System<ProjectilePredictionSystem>().TryLaunchSpell("MedievalProjectileFireballBeginner", player,
                    key, ce.GetComponent<TransformComponent>(player).Coordinates, Vector2.UnitX), Is.True);
            });
        }
        // Deliberately keep the server projectile unspawned, so no server impact can provide this feedback.
        await pair.RunTicksSync(15);
        var clientTarget = ce.GetEntity(se.GetNetEntity(target));
        var predictedWaves = new HashSet<EntityUid>();
        var predictedBlasts = new HashSet<EntityUid>();
        var predictedFireballSounds = new HashSet<EntityUid>();
        await client.WaitAssertion(() =>
        {
            Assert.That(Visuals(ce, map.MapId, true).Count, Is.EqualTo(predict ? 1 : 0));
            Assert.That(Entities<ShockWaveDistortionComponent>(ce, true, map.MapId).Count, Is.EqualTo(predict ? 1 : 0));
            Assert.That(BlastSounds(ce, map.MapId).Count, Is.EqualTo(predict ? 1 : 0));
            Assert.That(FireballSounds(ce, map.MapId).Count, Is.EqualTo(predict ? 1 : 0));
            predictedWaves.UnionWith(Entities<ShockWaveDistortionComponent>(ce, true, map.MapId));
            predictedBlasts.UnionWith(BlastSounds(ce, map.MapId));
            predictedFireballSounds.UnionWith(FireballSounds(ce, map.MapId));
            Assert.That(ce.GetComponent<DamageableComponent>(clientTarget).TotalDamage.Float(), Is.Zero);
            if (predict)
            {
                var visual = Visuals(ce, map.MapId, true).Single();
                var textures = ce.GetComponent<ExplosionVisualsTexturesComponent>(visual);
                Assert.That(ce.EntityExists(textures.LightEntity), Is.True);
                Assert.That(textures.FireFrames, Is.Not.Empty);
            }
        });
        await server.WaitAssertion(() =>
            Assert.That(se.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.Zero));
        EntityUid projectile = default;
        await server.WaitPost(() =>
        {
            projectile = se.SpawnEntity("MedievalProjectileFireballBeginner", map.GridCoords);
            server.System<SharedProjectileSystem>().MarkPrediction(projectile, caster, key);
            server.System<SharedGunSystem>().ShootProjectile(projectile, Vector2.UnitX, Vector2.Zero, caster, caster);
        });
        await pair.RunTicksSync(15);
        await server.WaitAssertion(() =>
        {
            Assert.That(se.EntityExists(projectile), Is.False);
            Assert.That(se.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.GreaterThan(0));
            var visual = Visuals(se, map.MapId, false).Single();
            Assert.That(se.GetComponent<ExplosionVisualsComponent>(visual).Predictions.Single().Key, Is.EqualTo(key));
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(Visuals(ce, map.MapId, true), Is.Empty, "The provisional footprint must hand over.");
            Assert.That(Visuals(ce, map.MapId, false).Count, Is.EqualTo(1));
            if (predict)
            {
                // The native 0.8-second wave/sound may have expired during this deliberately delayed confirmation.
                // Confirmation must not create replacements even after the original feedback has finished.
                Assert.That(Entities<ShockWaveDistortionComponent>(ce, true, map.MapId), Is.SubsetOf(predictedWaves));
                Assert.That(BlastSounds(ce, map.MapId), Is.SubsetOf(predictedBlasts));
                Assert.That(FireballSounds(ce, map.MapId), Is.SubsetOf(predictedFireballSounds));
            }
            else
            {
                Assert.That(Entities<ShockWaveDistortionComponent>(ce, true, map.MapId).Count, Is.EqualTo(1));
                Assert.That(BlastSounds(ce, map.MapId).Count, Is.EqualTo(1));
                Assert.That(FireballSounds(ce, map.MapId).Count, Is.EqualTo(1));
            }
            Assert.That(BlastSounds(ce, map.MapId, false), Is.Empty, "The caster must not also receive unkeyed native audio.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CombinedExplosionsKeepAllShotIdentitiesAndReplaceBothForecasts()
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
        var keys = new[]
        {
            new ProjectilePredictionKey(se.GetNetEntity(caster), new GameTick(202), 0),
            new ProjectilePredictionKey(se.GetNetEntity(caster), new GameTick(202), 1),
        };
        await client.WaitPost(() =>
        {
            var coords = ce.GetComponent<TransformComponent>(client.Session!.AttachedEntity!.Value).Coordinates.Offset(new Vector2(2, 0));
            foreach (var key in keys)
            {
                var projectile = ce.SpawnEntity("MedievalProjectileFireballBeginner", coords);
                client.System<ClientExplosions>().PredictProjectileExplosion(projectile, key);
                ce.DeleteEntity(projectile);
            }
            Assert.That(Visuals(ce, map.MapId, true).Count, Is.EqualTo(2));
            Assert.That(BlastSounds(ce, map.MapId).Count, Is.EqualTo(2));
        });
        await server.WaitPost(() =>
        {
            foreach (var key in keys)
            {
                var projectile = se.SpawnEntity("MedievalProjectileFireballBeginner", map.GridCoords.Offset(new Vector2(2, 0)));
                server.System<SharedProjectileSystem>().MarkPrediction(projectile, caster, key);
                server.System<ServerExplosions>().TriggerExplosive(projectile);
            }
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var visual = Visuals(se, map.MapId, false).Single();
            Assert.That(se.GetComponent<ExplosionVisualsComponent>(visual).Predictions.Select(prediction => prediction.Key),
                Is.EquivalentTo(keys));
        });
        await client.WaitAssertion(() =>
        {
            Assert.That(Visuals(ce, map.MapId, true), Is.Empty);
            Assert.That(Visuals(ce, map.MapId, false).Count, Is.EqualTo(1));
            Assert.That(BlastSounds(ce, map.MapId).Count, Is.EqualTo(2));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ObserverReceivesOrdinaryBlastAudioAndTheSameShockwaveEffect()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var se = server.ResolveDependency<IEntityManager>();
        var ce = client.ResolveDependency<IEntityManager>();
        EntityUid shooter = default;
        await server.WaitPost(() =>
        {
            shooter = se.SpawnEntity("PredictionSpellTestCaster", map.GridCoords);
            var observer = se.SpawnEntity("PredictionSpellTestCaster", map.GridCoords.Offset(new Vector2(0, 2)));
            se.SpawnEntity("PredictionSpellTestTarget", map.GridCoords.Offset(new Vector2(2, 0)));
            var players = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
            players.SetAttachedEntity(players.Sessions.Single(), observer);
        });
        await pair.RunTicksSync(5);
        await server.WaitPost(() =>
        {
            var projectile = se.SpawnEntity("MedievalProjectileFireballBeginner", map.GridCoords);
            var key = new ProjectilePredictionKey(se.GetNetEntity(shooter), new GameTick(203), 0);
            server.System<SharedProjectileSystem>().MarkPrediction(projectile, shooter, key);
            server.System<SharedGunSystem>().ShootProjectile(projectile, Vector2.UnitX, Vector2.Zero, shooter, shooter);
        });
        await pair.RunTicksSync(15);
        await client.WaitAssertion(() =>
        {
            Assert.That(Visuals(ce, map.MapId, true), Is.Empty);
            Assert.That(Visuals(ce, map.MapId, false).Count, Is.EqualTo(1));
            Assert.That(BlastSounds(ce, map.MapId), Is.Empty);
            Assert.That(BlastSounds(ce, map.MapId, false).Count, Is.EqualTo(1));
            Assert.That(Entities<ShockWaveDistortionComponent>(ce, true, map.MapId).Count, Is.EqualTo(1));
            Assert.That(FireballSounds(ce, map.MapId).Count, Is.EqualTo(1));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LargeAndInvalidForecastsAreBounded()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        await pair.Client.WaitAssertion(() =>
        {
            var explosions = pair.Client.System<ClientExplosions>();
            Assert.That(explosions.GetPredictedVisuals(map.MapCoords, "Default", 0, 1, 1), Is.Null);
            Assert.That(explosions.GetPredictedVisuals(map.MapCoords, "Default", float.NaN, 1, 1), Is.Null);
            Assert.That(explosions.GetPredictedVisuals(map.MapCoords, "Default", 10, 0, 1), Is.Null);
            Assert.That(explosions.GetPredictedVisuals(map.MapCoords, "Default", float.MaxValue, 1, 1), Is.Null);
        });
        await pair.CleanReturnAsync();
    }

    private static List<EntityUid> Entities<T>(IEntityManager entities, bool clientSide, MapId? map = null) where T : IComponent
    {
        var result = new List<EntityUid>();
        var query = entities.EntityQueryEnumerator<T>();
        while (query.MoveNext(out var uid, out _))
            if (entities.IsClientSide(uid) == clientSide &&
                (map == null || entities.GetComponent<TransformComponent>(uid).MapID == map))
                result.Add(uid);
        return result;
    }

    private static List<EntityUid> Visuals(IEntityManager entities, MapId map, bool clientSide) =>
        Entities<ExplosionVisualsComponent>(entities, clientSide)
            .Where(uid => entities.GetComponent<ExplosionVisualsComponent>(uid).Epicenter.MapId == map).ToList();

    private static List<EntityUid> BlastSounds(IEntityManager entities, MapId map, bool clientSide = true) => Entities<AudioComponent>(entities, clientSide, map)
        .Where(uid =>
        {
            var file = entities.GetComponent<AudioComponent>(uid).FileName;
            return file.StartsWith("/Audio/Effects/explosion");
        }).ToList();

    private static List<EntityUid> FireballSounds(IEntityManager entities, MapId map) => Entities<AudioComponent>(entities, true, map)
        .Where(uid => entities.GetComponent<AudioComponent>(uid).FileName == "/Audio/Imperial/Medieval/fireball_blast.ogg").ToList();
}
// spellward end
