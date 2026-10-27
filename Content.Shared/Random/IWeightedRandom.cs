using Robust.Shared.Prototypes;

namespace Content.Shared.Random;

/// <summary>
/// IWeightedRandomPrototype implements a dictionary of strings to float weights
/// to be used with <see cref="Helpers.SharedRandomExtensions.Pick(IWeightedRandomPrototype, Robust.Shared.Random.IRobustRandom)" />.
/// </summary>
public interface IWeightedRandomPrototype : IPrototype
{
    [ViewVariables]
    public Dictionary<string, float> Weights { get; }
}

// spellward start - upstream typed counterpart retains prototype-ID validation.
public interface IWeightedRandomPrototype<TProto> : IPrototype
    where TProto : class, IPrototype
{
    [ViewVariables]
    public Dictionary<ProtoId<TProto>, float> Weights { get; }
}
// spellward end
