using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.CombatArts.Movement;

[RegisterComponent]
public sealed partial class FullMovementComponent : Component
{
    /// <summary>
    /// Default speed used while moving between points.
    /// </summary>
    [DataField]
    public float Speed = 12f;

    /// <summary>
    /// Minimum number of points the player must select.
    /// </summary>
    [DataField]
    public int MinPoints = 1;

    /// <summary>
    /// Maximum number of points the player may select.
    /// </summary>
    [DataField]
    public int MaxPoints = 1;

    /// <summary>
    /// Prevent normal player movement input while FullMovement is active.
    /// </summary>
    [DataField]
    public bool LockMovement = true;

    /// <summary>
    /// Prevent health damage while FullMovement is active.
    /// </summary>
    [DataField]
    public bool DamageImmune = false;

    /// <summary>
    /// Whether the movement may pass through wall-type obstacles.
    /// </summary>
    [DataField]
    public bool PassThroughWalls = false;

    /// <summary>
    /// Whether the movement may pass through other mobs/players.
    /// </summary>
    [DataField]
    public bool PassThroughMobs = false;
}