using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Xenoarchaeology.Artifact.Prototypes;
using NUnit.Framework;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Tests.Shared.Utility;

[TestFixture]
public sealed class TypedWeightedRandomTests
{
    [TestCase(1)]
    [TestCase(271)]
    public void TypedTablesKeepTheSameSeededSelections(int seed)
    {
        ComparePicks(new WeightedRandomEntityPrototype(), seed);
        ComparePicks(new WeightedRandomOrePrototype(), seed);
        ComparePicks(new WeightedRandomSpeciesPrototype(), seed);
        ComparePicks(new WeightedRandomXenoArchTriggerPrototype(), seed);
    }

    private static void ComparePicks<T>(IWeightedRandomPrototype<T> typed, int seed)
        where T : class, IPrototype
    {
        var legacy = new WeightedRandomPrototype();
        typed.Weights.Add("A", 1);
        typed.Weights.Add("B", 3);
        typed.Weights.Add("C", 7);
        legacy.Weights.Add("A", 1);
        legacy.Weights.Add("B", 3);
        legacy.Weights.Add("C", 7);

        var typedRandom = new RobustRandom();
        var legacyRandom = new RobustRandom();
        typedRandom.SetSeed(seed);
        legacyRandom.SetSeed(seed);

        for (var i = 0; i < 128; i++)
            Assert.That(typed.Pick(typedRandom).Id, Is.EqualTo(legacy.Pick(legacyRandom)));
    }
}
