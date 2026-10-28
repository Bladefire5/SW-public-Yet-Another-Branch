using System.Linq;
using Content.Shared.Imperial.LocalLight;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class LocalLightMigrationTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ExperimentLocalLightOwner
  components:
  - type: LocalLight
    radius: 4
    energy: 2
- type: entity
  id: ExperimentLocalLightOwnerWithServerLight
  parent: ExperimentLocalLightOwner
  components:
  - type: PointLight
    radius: 7
    energy: 3
";

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task PersonalLightRespectsNetworkOwnershipAndOwnerLifetime(bool serverLight, bool deleteOwner)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var entities = client.EntMan;
        EntityUid owner = default;
        await server.WaitPost(() =>
        {
            owner = server.EntMan.SpawnEntity(serverLight
                ? "ExperimentLocalLightOwnerWithServerLight"
                : "ExperimentLocalLightOwner", map.GridCoords);
        });
        await pair.RunTicksSync(5);
        var clientOwner = pair.ToClientUid(owner);
        PointLightComponent? serverLightComponent = null;
        bool serverLightNetSync = default;
        await client.WaitAssertion(() =>
        {
            Assert.That(ChildLights(entities, clientOwner), Is.Empty, "Uncontrolled actors need no personal light.");
            if (serverLight)
            {
                serverLightComponent = entities.GetComponent<PointLightComponent>(clientOwner);
                serverLightNetSync = serverLightComponent.NetSyncEnabled;
            }
        });

        await server.WaitPost(() => server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), owner));
        await pair.RunTicksSync(5);
        EntityUid light = default;
        await client.WaitAssertion(() =>
        {
            light = ChildLights(entities, clientOwner).Single();
            var personal = entities.GetComponent<PointLightComponent>(light);
            Assert.That(personal.NetSyncEnabled, Is.False);
            Assert.That(personal.Radius, Is.EqualTo(4));
            Assert.That(personal.Enabled, Is.True);
            AssertServerLight();
        });

        await server.WaitPost(() => server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), null));
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(light), Is.False);
            Assert.That(ChildLights(entities, clientOwner), Is.Empty);
            AssertServerLight();
        });

        await server.WaitPost(() => server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), owner));
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() => light = ChildLights(entities, clientOwner).Single());
        await server.WaitPost(() =>
        {
            if (deleteOwner)
                server.EntMan.DeleteEntity(owner);
            else
                server.EntMan.RemoveComponent<LocalLightComponent>(owner);
        });
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(light), Is.False);
            if (deleteOwner)
                Assert.That(entities.EntityExists(clientOwner), Is.False);
            else
                AssertServerLight();
        });
        await pair.CleanReturnAsync();

        void AssertServerLight()
        {
            if (!serverLight)
            {
                Assert.That(entities.HasComponent<PointLightComponent>(clientOwner), Is.False);
                return;
            }

            var current = entities.GetComponent<PointLightComponent>(clientOwner);
            Assert.That(current, Is.SameAs(serverLightComponent));
            Assert.That(current.NetSyncEnabled, Is.EqualTo(serverLightNetSync));
            Assert.That(current.Radius, Is.EqualTo(7));
        }
    }

    private static EntityUid[] ChildLights(IEntityManager entities, EntityUid owner)
    {
        return entities.EntityQuery<PointLightComponent>()
            .Where(light => entities.GetComponent<TransformComponent>(light.Owner).ParentUid == owner)
            .Select(light => light.Owner).ToArray();
    }
}
