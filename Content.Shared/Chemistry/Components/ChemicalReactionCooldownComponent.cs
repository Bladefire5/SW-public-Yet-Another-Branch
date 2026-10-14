//Spellward
using Content.Shared.Chemistry.Reaction;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Chemistry.Components;

/// <summary>
/// Cooldown state for reactions in one solution. Attach to the solution entity,
/// rather than its container, so different solutions have independent timers.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(ChemicalReactionSystem))]
public sealed partial class ChemicalReactionCooldownComponent : Component
{
    /// <summary>
    /// Activation deadlines indexed by a reaction or shared cooldown-group key.
    /// The chemistry system removes expired entries and resumes eligible reactions.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, ChemicalReactionCooldown> Cooldowns = new();

    /// <summary>
    /// True only while this solution's reaction loop is running. This temporary
    /// flag is neither saved nor networked and must be cleared in a finally block.
    /// </summary>
    [ViewVariables]
    public bool Processing;
}

/// <summary>
/// A reaction deadline. The offset serializer preserves the remaining delay
/// when the solution entity is saved and loaded at a different game time.
/// Runtime pause adjustments are performed by the chemistry system.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public partial struct ChemicalReactionCooldown
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextActivation;

    public ChemicalReactionCooldown(TimeSpan nextActivation)
    {
        NextActivation = nextActivation;
    }
}
