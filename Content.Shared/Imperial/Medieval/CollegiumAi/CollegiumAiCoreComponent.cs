using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.CollegiumAi;

/// <summary>
/// The statue the Collegium's watcher is bound to. Plays the same role as the station AI core: the watcher spawns
/// inside it, treats it as home, and is banished from the round if it is destroyed.
/// </summary>
[RegisterComponent]
public sealed partial class CollegiumAiCoreComponent : Component
{
    /// <summary>
    /// The watcher currently bound to this statue, if any.
    /// </summary>
    [ViewVariables]
    public EntityUid? Watcher;

    /// <summary>
    /// Faction whose members the bound watcher is allowed to follow.
    /// </summary>
    [DataField]
    public ProtoId<MedievalFactionPrototype> Faction = "Collegium";

    /// <summary>
    /// Name of the container the watcher spawns into.
    /// </summary>
    public const string Container = "collegium_ai_mind_slot";
}
