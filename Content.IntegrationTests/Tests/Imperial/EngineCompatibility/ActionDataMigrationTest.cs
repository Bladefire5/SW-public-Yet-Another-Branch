using System.Linq;
using Content.Shared.Actions.Components;
using Content.Shared.Charges.Components;
using Content.Shared.Magic.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class ActionDataMigrationTest
{
    [Test]
    public async Task AllActionPrototypesHaveUpstreamDependencies()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var factory = pair.Server.EntMan.ComponentFactory;
            Assert.Multiple(() =>
            {
                foreach (var prototype in prototypes.EnumeratePrototypes<EntityPrototype>())
                {
                    var targeted = prototype.HasComp<WorldTargetActionComponent>(factory)
                        || prototype.HasComp<EntityTargetActionComponent>(factory);
                    if (!targeted && !prototype.HasComp<InstantActionComponent>(factory))
                        continue;

                    Assert.That(prototype.HasComp<ActionComponent>(factory), Is.True, prototype.ID);
                    if (targeted)
                        Assert.That(prototype.HasComp<TargetActionComponent>(factory), Is.True, prototype.ID);
                }
            });
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("MedievalActionBarrier", 10, false, 0)]
    [TestCase("MedievalActionFireball1", 180, true, 60)]
    [TestCase("MedievalActionSunstrikeSenior", 90, true, 60)]
    [TestCase("MedievalActionIllitidForceTalk", 60, true, 15)]
    public async Task LegacyActionsInitializeWithConfiguredCooldownAndTargeting(
        string prototype, int cooldown, bool targeted, int range)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var action = entities.SpawnEntity(prototype, map.GridCoords);
            var component = entities.GetComponent<ActionComponent>(action);
            Assert.That(component.UseDelay, Is.EqualTo(TimeSpan.FromSeconds(cooldown)));
            Assert.That(component.Icon, Is.Not.Null);
            if (targeted)
            {
                var target = entities.GetComponent<TargetActionComponent>(action);
                Assert.That(target.Range, Is.EqualTo(range));
                Assert.That(target.CheckCanAccess, Is.False);
            }
            entities.DeleteEntity(action);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EverySpellbookInitializesItsActionsAndPreservesUnlimitedCharges()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var books = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(p => !p.Abstract && p.HasComp<SpellbookComponent>(entities.ComponentFactory)).ToArray();
            Assert.That(books, Is.Not.Empty);
            foreach (var prototype in books)
            {
                var book = entities.SpawnEntity(prototype.ID, map.GridCoords);
                var component = entities.GetComponent<SpellbookComponent>(book);
                Assert.That(component.Spells.Count, Is.EqualTo(component.SpellActions.Count), prototype.ID);
                foreach (var spell in component.Spells)
                {
                    Assert.That(entities.HasComponent<ActionComponent>(spell), Is.True, prototype.ID);
                    var id = entities.GetComponent<MetaDataComponent>(spell).EntityPrototype!.ID;
                    if (component.SpellActions[id] == null)
                        Assert.That(entities.HasComponent<LimitedChargesComponent>(spell), Is.False, prototype.ID);
                }
                entities.DeleteEntity(book);
            }
        });
        await pair.CleanReturnAsync();
    }
}
