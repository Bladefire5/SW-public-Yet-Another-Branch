using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.ChemistryRandomization;

[Virtual]
public class ReactionData : IComparable<ReactionData>
{
    [DataField("name")]
    public string Name = string.Empty;

    /// <summary>
    /// Reactants required for the reaction to occur.
    /// </summary>
    [DataField("reactants")]
    public Dictionary<ProtoId<ReagentPrototype>, ReactantPrototype> Reactants = new();

    /// <summary>
    ///     The minimum temperature the reaction can occur at.
    /// </summary>
    [DataField("minTemp")]
    public float MinimumTemperature = 0.0f;

    /// <summary>
    ///     If true, this reaction will attempt to conserve thermal energy.
    /// </summary>
    [DataField("conserveEnergy")]
    public bool ConserveEnergy = true;

    /// <summary>
    ///     The maximum temperature the reaction can occur at.
    /// </summary>
    [DataField("maxTemp")]
    public float MaximumTemperature = float.PositiveInfinity;

    /// <summary>
    ///     The required mixing categories for an entity to mix the solution with for the reaction to occur
    /// </summary>
    [DataField("requiredMixerCategories")]
    public List<ProtoId<MixingCategoryPrototype>>? MixingCategories;

    /// <summary>
    /// Reagents created when the reaction occurs.
    /// </summary>
    [DataField("products")]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Products = new();

    /// <summary>
    /// Effects to be triggered when the reaction occurs.
    /// </summary>
    [DataField("effects", serverOnly: true)] public List<EntityEffect> Effects = new();

    /// <summary>
    /// How dangerous is this effect? Stuff like bicaridine should be low, while things like methamphetamine
    /// or potas/water should be high.
    /// </summary>
    [DataField("impact", serverOnly: true)] public LogImpact Impact = LogImpact.Low;

    // TODO SERV3: Empty on the client, (de)serialize on the server with module manager is server module
    [DataField("sound", serverOnly: true)] public SoundSpecifier Sound { get; set; } = new SoundPathSpecifier("/Audio/Effects/Chemistry/bubbles.ogg");

    /// <summary>
    /// If true, this reaction will only consume only integer multiples of the reactant amounts. If there are not
    /// enough reactants, the reaction does not occur. Useful for spawn-entity reactions (e.g. creating cheese).
    /// </summary>
    [DataField("quantized")] public bool Quantized = false;

    /// <summary>
    /// Maximum reaction units processed in one activation. Each consumed reactant
    /// amount is its coefficient multiplied by these units. Null means uncapped.
    /// This is an upper limit, not a minimum amount required to react.
    /// </summary>
    [DataField("maxReactionUnits")]
    public FixedPoint2? MaxReactionUnits;

    /// <summary>
    /// Minimum time between activations of this reaction in the same solution.
    /// Zero preserves the existing immediate reaction behavior.
    /// </summary>
    [DataField("reactionCooldown")]
    public TimeSpan ReactionCooldown = TimeSpan.Zero;

    /// <summary>
    /// Reactions with the same non-empty group share a cooldown in one solution.
    /// An empty group uses a separate cooldown for each reaction.
    /// Explosive partner reactions use one shared group to pace their activations.
    /// </summary>
    [DataField("reactionCooldownGroup")]
    public string ReactionCooldownGroup = string.Empty;

    /// <summary>
    /// Determines the order in which reactions occur. This should used to ensure that (in general) descriptive /
    /// pop-up generating and explosive reactions occur before things like foam/area effects.
    /// </summary>
    [DataField("priority")]
    public int Priority;

    /// <summary>
    /// Determines whether or not this reaction creates a new chemical (false) or if it's a breakdown for existing chemicals (true)
    /// Used in the chemistry guidebook to make divisions between recipes and reaction sources.
    /// </summary>
    /// <example>
    /// Mixing together two reagents to get a third -> false
    /// Heating a reagent to break it down into 2 different ones -> true
    /// </example>
    [DataField]
    public bool Source;

    /// <summary>
    ///     Comparison for creating a sorted set of reactions. Determines the order in which reactions occur.
    /// </summary>
    public int CompareTo(ReactionData? other)
    {
        if (other == null)
            return -1;

        if (Priority != other.Priority)
            return other.Priority - Priority;

        // Prioritize reagents that don't generate products. This should reduce instances where a solution
        // temporarily overflows and discards products simply due to the order in which the reactions occurred.
        // Basically: Make space in the beaker before adding new products.
        if (Products.Count != other.Products.Count)
            return Products.Count - other.Products.Count;

        return string.Compare(Name, other.Name, StringComparison.Ordinal);
    }
}
