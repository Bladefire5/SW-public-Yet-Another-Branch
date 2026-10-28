using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.IntegrationTests.Tests.Chemistry;

[TestFixture]
[TestOf(typeof(ReactionPrototype))]
public sealed class TryAllReactionsTest
{
    // spellward - upstream reaction setup with this branch's solution-container prototype schema.
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestSolutionContainer
  components:
  - type: SolutionContainerManager
    solutions:
      beaker:
        maxVol: 120
        canMix: true";

    [Test]
    [TestOf(typeof(ReactionPrototype))]
    [Description("Tries an individual reaction to see if it succeeds.")]
    public async Task TryAllTest()
    {
        // spellward - adapt the pinned upstream test to the existing pooled fixture API.
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var solutionContainerSystem = server.System<SharedSolutionContainerSystem>();
        var testMap = await pair.CreateTestMap();
        var coordinates = testMap.GridCoords;

        // they call me a bird the way i be nesting
        try
        {
            foreach (var reactionPrototype in prototypeManager.EnumeratePrototypes<ReactionPrototype>())
            {
                Console.WriteLine($"Testing {reactionPrototype.ID}");

                EntityUid beaker = default;
                Solution solution = null;

                try
                {
                    await server.WaitAssertion(() =>
                    {
                        beaker = entityManager.SpawnEntity("TestSolutionContainer", coordinates);
                        Assert.That(solutionContainerSystem
                            .TryGetSolution(beaker, "beaker", out var solutionEnt, out solution));
                        solutionContainerSystem.SetCanReact(solutionEnt!.Value, false);
                        foreach (var (id, reactant) in reactionPrototype.Reactants)
                        {
#pragma warning disable NUnit2045 // Interdependent ingredient addition and accepted quantity.
                            Assert.That(solutionContainerSystem
                                .TryAddReagent(solutionEnt.Value,
                                    id,
                                    reactant.Amount,
                                    out var quantity,
                                    reactionPrototype.MinimumTemperature));
                            Assert.That(reactant.Amount, Is.EqualTo(quantity));
#pragma warning restore NUnit2045
                        }

                        //Get all possible reactions with the current reagents
                        var possibleReactions = prototypeManager.EnumeratePrototypes<ReactionPrototype>()
                            .Where(x => x.Reactants.All(id =>
                                solution.Contents.Any(s => s.Reagent.Prototype == id.Key)))
                            .ToList();

                        //Check if the reaction is the first to occur when heated
                        foreach (var possibleReaction in possibleReactions.OrderBy(r => r.MinimumTemperature))
                        {
                            if (possibleReaction.Priority >= reactionPrototype.Priority &&
                                possibleReaction.MinimumTemperature < reactionPrototype.MinimumTemperature &&
                                possibleReaction.MixingCategories == reactionPrototype.MixingCategories)
                            {
                                Assert.Fail(
                                    $"The {possibleReaction.ID} reaction may occur before {reactionPrototype.ID} when heated.");
                            }
                        }

                        //Check if the reaction is the first to occur when freezing
                        foreach (var possibleReaction in possibleReactions.OrderBy(r => r.MaximumTemperature))
                        {
                            if (possibleReaction.Priority >= reactionPrototype.Priority &&
                                possibleReaction.MaximumTemperature > reactionPrototype.MaximumTemperature &&
                                possibleReaction.MixingCategories == reactionPrototype.MixingCategories)
                            {
                                Assert.Fail(
                                    $"The {possibleReaction.ID} reaction may occur before {reactionPrototype.ID} when freezing.");
                            }
                        }

                        //Now safe set the temperature and mix the reagents
                        solutionContainerSystem.SetTemperature(solutionEnt.Value,
                            reactionPrototype.MinimumTemperature);
                        solutionContainerSystem.SetCanReact(solutionEnt.Value, true);

                        if (reactionPrototype.MixingCategories != null)
                        {
                            var dummyEntity = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
                            var mixerComponent = entityManager.AddComponent<ReactionMixerComponent>(dummyEntity);
                            mixerComponent.ReactionTypes = reactionPrototype.MixingCategories;
                            solutionContainerSystem.UpdateChemicals(solutionEnt.Value, true, mixerComponent);
                        }
                    });

                    await server.WaitIdleAsync();

                    await server.WaitAssertion(() =>
                    {
                        //you just got linq'd fool
                        //(i'm sorry)
                        var foundProductsMap = reactionPrototype.Products.ToDictionary(x => x.Key.Id, x => x.Value) //spellward - string-facing reagent keys
                            .Concat(reactionPrototype.Reactants
                                .Where(x => x.Value.Catalyst)
                                .ToDictionary(x => x.Key, x => x.Value.Amount)
                            )
                            .ToDictionary(x => x, _ => false);

                        foreach (var (reagent, quantity) in solution.Contents)
                        {
                            Assert.That(foundProductsMap.TryFirstOrNull(
                                x => x.Key.Key == reagent.Prototype && x.Key.Value == quantity,
                                out var foundProduct), $"Reaction {reactionPrototype.ID}: unexpected {quantity} units of {reagent.Prototype}.");
                            foundProductsMap[foundProduct!.Value.Key] = true;
                        }

                        Assert.That(foundProductsMap.All(x => x.Value));
                    });
                }
                finally
                {
                    await server.WaitPost(() => entityManager.DeleteEntity(beaker));
                }
            }
        }
        finally
        {
            await server.WaitPost(() => entityManager.DeleteEntity(testMap.MapUid));
        }

        await pair.CleanReturnAsync(); //spellward
    }
}
