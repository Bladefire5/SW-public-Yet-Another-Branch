using Robust.Shared.Map;

namespace Content.Shared.Imperial.Medieval.CombatArts.Movement;

[RegisterComponent]
public sealed partial class ActiveFullMovementComponent : Component
{
    /// <summary>
    /// Ordered movement destinations.
    /// </summary>
    public List<EntityCoordinates> Points = new();

    /// <summary>
    /// Index of the point currently being travelled toward.
    /// </summary>
    public int CurrentPointIndex;

    /// <summary>
    /// Current movement speed.
    /// </summary>
    public float Speed;

    /// <summary>
    /// Whether normal movement input is currently disabled.
    /// </summary>
    public bool LockMovement;

    /// <summary>
    /// Whether health damage should currently be prevented.
    /// </summary>
    public bool DamageImmune;

    /// <summary>
    /// Whether walls may be crossed during this movement.
    /// </summary>
    public bool PassThroughWalls;

    /// <summary>
    /// Whether mobs may be crossed during this movement.
    /// </summary>
    public bool PassThroughMobs;

    /// <summary>
    /// Action that started this movement.
    /// </summary>
    public EntityUid? SourceAction;
}