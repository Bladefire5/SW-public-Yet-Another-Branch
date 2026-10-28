using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Server.Imperial.Medieval.GameTicking.Rules;
using Content.Shared.GameTicking.Components;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class RuleLifecycleMigrationTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: BaseGameRule
  id: ExperimentRoundstartOnlyAntag
  components:
  - type: AntagSelection
    selectionTime: PrePlayerSpawn
    definitions:
    - prefRoles: [Traitor]
      entityPrototype: MobHuman
      min: 1
      max: 1
";

    [Test]
    public async Task MidroundSelectionWithoutAGhostSpawnerDoesNotAssignOrSpawnAntags()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var ticker = pair.Server.System<GameTicker>();
            var rule = ticker.AddGameRule("ExperimentRoundstartOnlyAntag");
            var component = entities.GetComponent<AntagSelectionComponent>(rule);
            var before = entities.EntityCount;
            pair.Server.System<AntagSelectionSystem>().ChooseAntags((rule, component), [], midround: true);
            Assert.That(component.AssignedMinds, Is.Empty);
            Assert.That(component.PreSelectedSessions, Is.Empty);
            Assert.That(entities.EntityCount, Is.EqualTo(before));
            entities.DeleteEntity(rule);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NocturneRuleEndsCleanlyWithoutSpawnMarkers()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var ticker = server.System<GameTicker>();
            Assert.That(server.System<AncientNocturneSpawnRuleSystem>().CanStartNocturneEvent(), Is.False);
            Assert.That(ticker.StartGameRule("MedievalAncientNocturneSpawnRule", out var rule), Is.True);
            Assert.That(ticker.IsGameRuleActive(rule), Is.False);
            Assert.That(server.EntMan.HasComponent<EndedGameRuleComponent>(rule), Is.True);
            ticker.ClearGameRules();
            Assert.That(ticker.IsGameRuleAdded(rule), Is.False);
            server.EntMan.DeleteEntity(rule);
        });
        await pair.CleanReturnAsync();
    }
}
