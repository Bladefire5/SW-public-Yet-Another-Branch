using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.CombatArts.Movement;

/// <summary>
/// Configures a Full Movement world-target action.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FullMovementComponent : Component
{
    /// <summary>
    /// Desired movement speed while the dash is active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Speed = 10f;

    /// <summary>
    /// How quickly the requested Full Movement speed increases.
    /// 0 = immediately use full Speed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Acceleration = 0f;

    /// <summary>
    /// How quickly Full Movement slows down near the end.
    /// 0 = no automatic deceleration.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Deceleration = 0f;

    /// <summary>
    /// How close the performer must be to the target before the dash ends.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ArrivalDistance = 0.2f;

    /// <summary>
    /// How much the player can influence the dash direction.
    /// 0 = no control, 1 = full directional control.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Steering = 0f;

    /// <summary>
    /// Maximum steering turn speed in degrees per second.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TurnSpeed = 360f;

    [DataField, AutoNetworkedField]
    public float SteeringDistancePenalty = 0.25f;

    /// <summary>
    /// Whether Full Movement continuously corrects back toward the selected target.
    /// True = steering bends the path while still homing toward the target.
    /// False = steering changes the actual movement trajectory.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Homing = true;

    /// <summary>
    /// Multiplies the original target distance to determine the maximum
    /// distance Full Movement is allowed to travel.
    ///
    /// 1 = original target distance.
    /// 1.25 = 25% additional travel distance.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DistanceBudgetMultiplier = 1.25f;

    /// <summary>
    /// Whether Full Movement stops when the performer physically
    /// collides with a hard impassable object.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool StopOnWallContact = true;

    /// <summary>
    /// Extra distance ahead to check for walls before moving.
    /// 0 disables predictive wall stopping.
    ///
    /// The actual check also includes the distance Full Movement
    /// expects to travel during the next simulation tick.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WallDetectionDistance = 0.1f;

}