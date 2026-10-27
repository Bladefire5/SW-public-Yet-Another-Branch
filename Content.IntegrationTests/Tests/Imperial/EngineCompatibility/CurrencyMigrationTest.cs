using Content.Server.Imperial.ImperialStore;
using Content.Server.Imperial.Medieval.Trading;
using Content.Server.Store.Components;
using Content.Server.Store.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Stacks;
using Content.Shared.Store.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class CurrencyMigrationTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: currency
  id: ExperimentCurrency
- type: currency
  id: ExperimentOtherCurrency
- type: imperialCurrency
  id: ExperimentImperialCurrency
- type: stack
  id: ExperimentCurrencyStack
  spawn: ExperimentCurrencyCoin
  maxCount: 50
- type: entity
  id: ExperimentCurrencyCoin
  components:
  - type: Stack
    stackType: ExperimentCurrencyStack
    count: 3
  - type: Currency
    price:
      ExperimentCurrency: 2
  - type: MedievalCurrency
    price:
      ExperimentCurrency: 2
  - type: ImperialCurrency
    price:
      ExperimentImperialCurrency: 2
- type: entity
  id: ExperimentCurrencyStore
  components:
  - type: Store
    currencyWhitelist: [ExperimentCurrency]
  - type: UserInterface
    interfaces:
      enum.StoreUiKey.Key:
        type: StoreBoundUserInterface
- type: entity
  id: ExperimentCurrencyTradingPit
  components:
  - type: Trading
    currency: ExperimentCurrency
    guildTypes: []
  - type: UserInterface
    interfaces:
      enum.TradingUiKey.Key:
        type: TradingBoundUserInterface
";

    [TestCase(false, 1)]
    [TestCase(false, 3)]
    [TestCase(true, 1)]
    [TestCase(true, 3)]
    public async Task InsertingCurrencyCreditsWholeStackOnce(bool medieval, int count)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var coin = entities.SpawnEntity("ExperimentCurrencyCoin", map.GridCoords);
            server.System<SharedStackSystem>().SetCount(coin, count);
            var target = entities.SpawnEntity(medieval ? "ExperimentCurrencyTradingPit" : "ExperimentCurrencyStore", map.GridCoords);
            FixedPoint2 expected = 2 * count;

            if (medieval)
            {
                var currency = entities.GetComponent<MedievalCurrencyComponent>(coin);
                var store = entities.GetComponent<TradingComponent>(target);
                Assert.That(server.System<TradingSystem>().TryAddCurrency((coin, currency), (target, store)), Is.True);
                Assert.That(currency.Price, Is.Empty);
                server.System<TradingSystem>().TryAddCurrency((coin, currency), (target, store));
                Assert.That(store.Balance, Is.EqualTo(2 * count));
            }
            else
            {
                var currency = entities.GetComponent<CurrencyComponent>(coin);
                var store = entities.GetComponent<StoreComponent>(target);
                Assert.That(server.System<StoreSystem>().GetCurrencyValue(coin, currency)["ExperimentCurrency"], Is.EqualTo(expected));
                Assert.That(currency.Price["ExperimentCurrency"], Is.EqualTo((FixedPoint2) 2));
                Assert.That(server.System<StoreSystem>().TryAddCurrency((coin, currency), (target, store)), Is.True);
                Assert.That(currency.Price, Is.Empty);
                server.System<StoreSystem>().TryAddCurrency((coin, currency), (target, store));
                Assert.That(store.Balance["ExperimentCurrency"], Is.EqualTo(expected));
            }

            Assert.That(entities.GetComponent<StackComponent>(coin).Count, Is.Zero);
            Assert.That(entities.IsQueuedForDeletion(coin), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task UnsupportedCurrencyRejectsWholePaymentWithoutConsumingIt(bool medieval)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var coin = entities.SpawnEntity("ExperimentCurrencyCoin", map.GridCoords);
            var target = entities.SpawnEntity(medieval ? "ExperimentCurrencyTradingPit" : "ExperimentCurrencyStore", map.GridCoords);
            if (medieval)
            {
                var currency = entities.GetComponent<MedievalCurrencyComponent>(coin);
                currency.Price.Add("ExperimentOtherCurrency", 5);
                var store = entities.GetComponent<TradingComponent>(target);
                Assert.That(server.System<TradingSystem>().TryAddCurrency((coin, currency), (target, store)), Is.False);
                Assert.That(store.Balance, Is.Zero);
                Assert.That(currency.Price.Count, Is.EqualTo(2));
            }
            else
            {
                var currency = entities.GetComponent<CurrencyComponent>(coin);
                currency.Price.Add("ExperimentOtherCurrency", 5);
                var store = entities.GetComponent<StoreComponent>(target);
                Assert.That(server.System<StoreSystem>().TryAddCurrency((coin, currency), (target, store)), Is.False);
                Assert.That(store.Balance, Is.Empty);
                Assert.That(currency.Price.Count, Is.EqualTo(2));
            }

            Assert.That(entities.GetComponent<StackComponent>(coin).Count, Is.EqualTo(3));
            Assert.That(entities.IsQueuedForDeletion(coin), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(1)]
    [TestCase(3)]
    public async Task ImperialCurrencyProjectionKeepsIdAndStackValue(int count)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var coin = server.EntMan.SpawnEntity("ExperimentCurrencyCoin", map.GridCoords);
            server.System<SharedStackSystem>().SetCount(coin, count);
            var currency = server.EntMan.GetComponent<ImperialCurrencyComponent>(coin);
            var value = server.System<ImperialStoreSystem>().GetCurrencyValue(coin, currency);
            Assert.That(value["ExperimentImperialCurrency"], Is.EqualTo((FixedPoint2) (2 * count)));
            Assert.That(currency.Price["ExperimentImperialCurrency"], Is.EqualTo((FixedPoint2) 2));
        });

        await pair.CleanReturnAsync();
    }
}
