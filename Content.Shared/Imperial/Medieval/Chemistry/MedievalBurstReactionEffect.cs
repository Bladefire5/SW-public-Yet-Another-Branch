using System.Collections.Generic;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Chemistry;

public enum MedievalBurstFalloff : byte
{
    /// <summary>
    /// All targets within the configured radius receive the same strength.
    /// </summary>
    None,

    /// <summary>
    /// Strength decreases linearly from the center to zero at the radius.
    /// </summary>
    Linear,
}

/// <summary>
/// Requests an area burst of ordinary chemical effects for radiation,
/// Cold and Caustic explosive reactions. The initial blast is separate.
/// Requires a server handler for ExecuteEntityEffectEvent of this type.
/// </summary>
[DataDefinition]
public sealed partial class MedievalBurstReactionEffect : EventEntityEffect<MedievalBurstReactionEffect>
{
    /// <summary>
    /// Burst radius in world units; configured independently of the initial blast.
    /// </summary>
    [DataField("radius", required: true)]
    public float Radius;

    [DataField("falloff", required: true)]
    public MedievalBurstFalloff Falloff;

    /// <summary>
    /// Existing chemical damage/status effects, configured for full strength.
    /// The server handler supplies each target and the activation/falloff scale.
    /// </summary>
    [DataField("effects", required: true)]
    public List<EntityEffect> Effects = new();

    public override bool ShouldLog => true;
    public override LogImpact LogImpact => LogImpact.High;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype,
        IEntitySystemManager entSys)
        => null;
}
