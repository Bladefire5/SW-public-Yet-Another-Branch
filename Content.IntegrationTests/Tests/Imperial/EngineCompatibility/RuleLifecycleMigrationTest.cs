using Content.Server.GameTicking;
using Content.Server.Imperial.Medieval.GameTicking.Rules;
using Content.Shared.GameTicking.Components;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class RuleLifecycleMigrationTest
{
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
