using Robust.Shared.GameStates;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Shared.Imperial.Medieval.CombatArts.Movement;

/// <summary>
/// Temporary state placed on an entity while it is performing Full Movement.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ActiveFullMovementComponent : Component
{

    /// <summary>
    /// Whether Full Movement is currently overriding movement.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsActive;
    /// <summary>
    /// World target the entity is moving toward.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityCoordinates Target;

    /// <summary>
    /// Desired movement speed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Speed;

    /// <summary>
    /// Maximum acceleration of the requested movement speed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Acceleration;

    /// <summary>
    /// Maximum deceleration of the requested movement speed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Deceleration;

    /// <summary>
    /// Current requested movement speed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CurrentSpeed;

    /// <summary>
    /// Distance from the target at which Full Movement ends.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ArrivalDistance = 0.2f;

    /// <summary>
    /// How much the player can steer while Full Movement is active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Steering;

    /// <summary>
    /// Maximum steering turn speed in degrees per second.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TurnSpeed = 360f;

    /// <summary>
    /// Extra travel-distance cost caused by steering.
    /// Only used for non-homing movement.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SteeringDistancePenalty;

    /// <summary>
    /// Whether this movement keeps correcting toward its original target.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Homing = true;

    /// <summary>
    /// Current travel direction for non-homing Full Movement.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 CurrentDirection;

    /// <summary>
    /// Remaining travel distance for non-homing Full Movement.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RemainingDistance;

    /// <summary>
    /// Previous world position, used to measure distance actually traveled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 LastPosition;
}