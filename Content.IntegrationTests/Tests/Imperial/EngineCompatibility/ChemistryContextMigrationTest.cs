using System.Linq;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Medieval.ChemistryRandomization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Imperial.EngineCompatibility;

[TestFixture]
public sealed class ChemistryContextMigrationTest
{
    [Test]
    public async Task PotionRecipesAndAppearanceBelongToEachGameContext()
    {
        // Disconnected contexts let us supply seeds without the normal synchronization message.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        string serverRecipes = string.Empty;
        string clientRecipes = string.Empty;

        await pair.Server.WaitAssertion(() =>
        {
            var chemistry = pair.Server.System<SharedChemistryRandomizationSystem>();
            chemistry.Seed = 3417;
            chemistry.GeneratePotions();
            serverRecipes = Snapshot(pair.Server.ResolveDependency<IPrototypeManager>());
        });

        await pair.Client.WaitAssertion(() =>
        {
            var chemistry = pair.Client.System<SharedChemistryRandomizationSystem>();
            chemistry.Seed = 3417;
            chemistry.GeneratePotions();
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            Assert.That(Snapshot(prototypes), Is.EqualTo(serverRecipes), "The same seed must synchronize recipes and appearance.");

            chemistry.Seed = 9898;
            chemistry.GeneratePotions();
            clientRecipes = Snapshot(prototypes);
            Assert.That(clientRecipes, Is.Not.EqualTo(serverRecipes));
        });

        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            Assert.That(Snapshot(prototypes), Is.EqualTo(serverRecipes), "Client generation must not alter server recipes or prototype presentation.");

            var chemistry = pair.Server.System<SharedChemistryRandomizationSystem>();
            chemistry.GeneratePotions();
            Assert.That(Snapshot(prototypes), Is.EqualTo(serverRecipes), "Repeated generation with the same seed must be deterministic.");
        });

        await pair.Client.WaitAssertion(() =>
            Assert.That(Snapshot(pair.Client.ResolveDependency<IPrototypeManager>()), Is.EqualTo(clientRecipes),
                "Server regeneration must not alter client recipes or prototype presentation."));

        await pair.CleanReturnAsync();
    }

    private static string Snapshot(IPrototypeManager prototypes)
    {
        var potions = prototypes.EnumeratePrototypes<ChemistryRandomizationGroupPrototype>()
            .SelectMany(group => group.Potions).Distinct().OrderBy(id => id);
        return string.Join("\n", potions.Select(id =>
        {
            var reagent = prototypes.Index<ReagentPrototype>(id);
            var reactions = SharedChemistryRandomizationSystem.GetReactionsOrNull(reagent);
            Assert.That(reactions, Is.Not.Null.And.Not.Empty, $"Missing generated recipes for {id}.");
            var recipes = string.Join(";", reactions!.Select(reaction =>
                string.Join(",", reaction.Reactants.Select(input => $"{input.Key}:{input.Value.Amount.Value}:{input.Value.Catalyst}")) +
                "=>" + string.Join(",", reaction.Products.Select(output => $"{output.Key}:{output.Value.Value}"))));
            return $"{id}|{reagent.LocalizedName}|{SharedChemistryRandomizationSystem.GetStaticName(reagent)}|" +
                   $"{reagent.LocalizedPhysicalDescription}|{SharedChemistryRandomizationSystem.GetColor(reagent)}|" +
                   $"{SharedChemistryRandomizationSystem.GetFlavor(reagent)}|{recipes}";
        }));
    }
}
