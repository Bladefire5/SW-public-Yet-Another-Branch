using Content.Shared.DoAfter;
using Content.Shared.Storage;
using Content.Shared.Trade;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class ComponentLifecycleMigrationTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ExperimentBareBossHeal
  components:
  - type: TimedBossHeal
- type: entity
  id: ExperimentBareBossCharge
  components:
  - type: ChargingRuneExplosion
- type: entity
  id: ExperimentBareTradeTerminal
  components:
  - type: TradeTerminal
- type: entity
  parent: BaseStorageItem
  id: ExperimentTradeOfferTerminal
  components:
  - type: TradeTerminal
  - type: Storage
    maxItemSize: Small
    grid:
    - 0,0,1,1
- type: entity
  id: ExperimentTradeOfferItem
  components:
  - type: Item
";

    [TestCase("ExperimentBareBossHeal")]
    [TestCase("ExperimentBareBossCharge")]
    public async Task BossTimersInitializeTheirDoAfterDependency(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var owner = entities.SpawnEntity(prototype, map.GridCoords);
            Assert.That(entities.GetComponent<DoAfterComponent>(owner).DoAfters.Count, Is.EqualTo(1));
            entities.DeleteEntity(owner);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TradeTerminalWithoutAContainerDeletesCleanly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var terminal = entities.SpawnEntity("ExperimentBareTradeTerminal", map.GridCoords);
            entities.DeleteEntity(terminal);
            Assert.That(entities.Deleted(terminal), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovingTradeTerminalStillReturnsItsOfferedItems()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var terminal = entities.SpawnEntity("ExperimentTradeOfferTerminal", map.GridCoords);
            var item = entities.SpawnEntity("ExperimentTradeOfferItem", map.GridCoords);
            // Offers can only be inserted while a trade session is active.
            entities.GetComponent<TradeTerminalComponent>(terminal).State = TradeSessionState.Active;
            var container = entities.GetComponent<StorageComponent>(terminal).Container;
            Assert.That(pair.Server.System<SharedContainerSystem>().Insert(item, container), Is.True);
            entities.RemoveComponent<TradeTerminalComponent>(terminal);
            Assert.That(container.ContainedEntities, Is.Empty);
            Assert.That(entities.Deleted(item), Is.False);
            Assert.That(entities.GetComponent<TransformComponent>(item).GridUid, Is.EqualTo(map.Grid.Owner));
            entities.DeleteEntity(terminal);
            entities.DeleteEntity(item);
        });
        await pair.CleanReturnAsync();
    }
}
