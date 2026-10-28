using Content.Shared.Containers.ItemSlots;
using Content.Shared.Storage;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class StorageFillMigrationTest
{
    [TestCase("BoxLatexGloves", 4)]
    [TestCase("BoxNitrileGloves", 4)]
    [TestCase("MedalCase", 8)]
    [TestCase("PlushieIAAGift", 2)]
    [TestCase("ClothingBackpackChameleonFill", 11)]
    [TestCase("BriefcaseSyndieLobbyingBundleFilled", 10)]
    [TestCase("BriefcaseSyndieMimeFilled", 5)]
    public async Task FilledStorageRetainsEveryConfiguredItem(string prototype, int count)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var storage = entities.SpawnEntity(prototype, map.GridCoords);
            Assert.That(entities.GetComponent<StorageComponent>(storage).Container.ContainedEntities.Count,
                Is.EqualTo(count), prototype);
            entities.DeleteEntity(storage);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("ClothingShoesCaptainPirateBoots")]
    [TestCase("ClothingShoesPirateBootsHorizon")]
    public async Task PirateBootsUseTheUpstreamMilitaryBootItemSlot(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var boots = entities.SpawnEntity(prototype, map.GridCoords);
            var knife = entities.GetComponent<ItemSlotsComponent>(boots).Slots["item"].Item;
            Assert.That(knife, Is.Not.Null);
            Assert.That(entities.GetComponent<MetaDataComponent>(knife!.Value).EntityPrototype!.ID,
                Is.EqualTo("CombatKnife"));
            entities.DeleteEntity(boots);
        });
        await pair.CleanReturnAsync();
    }
}
