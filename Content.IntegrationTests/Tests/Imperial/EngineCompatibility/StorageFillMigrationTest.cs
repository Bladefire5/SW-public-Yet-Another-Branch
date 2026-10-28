using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

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

    [TestCase("MedievalDecorCastle1")]
    [TestCase("MedievalDecorCastle2")]
    public async Task LootStandCanFitEveryPossibleLootRoll(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var stand = entities.SpawnEntity(prototype, map.GridCoords);
            var storage = entities.GetComponent<StorageComponent>(stand);
            foreach (var rolledItem in storage.Container.ContainedEntities.ToArray())
                entities.DeleteEntity(rolledItem);

            var fill = (StorageFillComponent) pair.Server.ProtoMan.Index<EntityPrototype>(prototype)
                .Components["StorageFill"].Component;
            var storageSystem = pair.Server.System<SharedStorageSystem>();
            var count = 0;
            foreach (var entry in fill.Contents)
            {
                for (var i = 0; i < Math.Max(entry.Amount, entry.MaxAmount); i++)
                {
                    var item = entities.SpawnEntity(entry.PrototypeId!.Value.ToString(), map.GridCoords);
                    Assert.That(storageSystem.TryGetAvailableGridSpace((stand, storage), (item, null), out var location),
                        Is.True, $"{prototype} has no space for {entry.PrototypeId}");
                    Assert.That(storageSystem.InsertAt((stand, storage), (item, null), location!.Value,
                        out _, playSound: false, stackAutomatically: false), Is.True);
                    count++;
                }
            }

            Assert.That(storage.Container.ContainedEntities.Count, Is.EqualTo(count));
            entities.DeleteEntity(stand);
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
