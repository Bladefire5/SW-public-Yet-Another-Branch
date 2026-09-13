using System.Numerics;
using Content.Shared.Imperial.Medieval.MobRiding;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Components;

namespace Content.Shared.Imperial.Medieval.CombatArts.Movement;

/// <summary>
/// Performs a simple targeted dash by overriding the normal mover's WishDir.
///
/// SharedMoverController still owns acceleration, friction, rotation,
/// footsteps, and SetLinearVelocity.
/// </summary>
public sealed class FullMovementSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FullMovementEvent>(OnFullMovement);

        SubscribeLocalEvent<ActiveFullMovementComponent, WishDirOverrideEvent>(
        OnWishDirOverride, after: [typeof(HorseMoverSystem)]);
    }

    /// <summary>
    /// Starts or replaces the performer's current Full Movement dash.
    /// </summary>
    private void OnFullMovement(FullMovementEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp<FullMovementComponent>(
            args.Action.Owner,
            out var movement))
        {
            return;
        }

        if (movement.Speed <= 0f)
            return;

        var current = _transformSystem.GetMapCoordinates(args.Performer);
        var target = _transformSystem.ToMapCoordinates(args.Target);

        if (current.MapId != target.MapId)
            return;

        var offset = target.Position - current.Position;

        if (offset == Vector2.Zero)
        {
            args.Cancelled = true;
            return;
        }

        var active = EnsureComp<ActiveFullMovementComponent>(
        args.Performer);

        active.IsActive = true;

        active.Target = args.Target;
        active.Speed = movement.Speed;
        active.Acceleration = MathF.Max(0f, movement.Acceleration);
        active.Deceleration = MathF.Max(0f, movement.Deceleration);
        active.CurrentSpeed = active.Acceleration > 0f ? 0f : active.Speed;
        active.ArrivalDistance = MathF.Max(0f, movement.ArrivalDistance);
        active.Steering = Math.Clamp(movement.Steering, 0f, 1f);
        active.Homing = movement.Homing;
        active.TurnSpeed = MathF.Max(0f, movement.TurnSpeed);
        active.SteeringDistancePenalty = MathF.Max(0f, movement.SteeringDistancePenalty);

        // Initial heading is always toward the selected target.
        active.CurrentDirection = offset.Normalized();

        // movement uses the originally selected distance
        // as its maximum travel distance.
        active.RemainingDistance = offset.Length() * MathF.Max(0f, movement.DistanceBudgetMultiplier);
        active.LastPosition = current.Position;
        active.LastTargetPosition = target.Position;

        Dirty(args.Performer, active);

        args.Handled = true;
    }

    /// <summary>
    /// Overrides normal movement while Full Movement is active.
    /// </summary>
    private void OnWishDirOverride(
        Entity<ActiveFullMovementComponent> ent,
        ref WishDirOverrideEvent args)
    {
        if (!ent.Comp.IsActive)
            return;

        var current = _transformSystem.GetMapCoordinates(ent.Owner);
        var target = _transformSystem.ToMapCoordinates(ent.Comp.Target);

        // Stop if the target is on another map.
        if (current.MapId != target.MapId)
        {
            StopFullMovement(ent);
            return;
        }

        var offset = target.Position - current.Position;

        Vector2 forcedDirection;

        var traveled = 0f;

        if (_timing.InSimulation)
        {
            traveled = Vector2.Distance(current.Position, ent.Comp.LastPosition);
        }

        // Start with the original dash direction as a fallback.
        var currentDirection = ent.Comp.CurrentDirection;

        // Prefer the actual predicted physics velocity.
        // SharedMoverController already owns and predicts this state.
        if (TryComp<PhysicsComponent>(
        ent.Owner,
        out var physics) &&
        physics.LinearVelocity.LengthSquared() > 0.01f)
        {
            currentDirection = physics.LinearVelocity.Normalized();
        }
        if (ent.Comp.Homing)
        {
            if (_timing.InSimulation)
            {
                // Measure the target relative to the performer on the
                // previous and current simulation ticks.
                //
                // This handles BOTH the performer and the target moving.
                var previousOffset = ent.Comp.LastTargetPosition - ent.Comp.LastPosition;

                var currentOffset = target.Position - current.Position;

                if (PassedArrivalRadius(previousOffset, currentOffset, ent.Comp.ArrivalDistance))
                {
                    StopFullMovement(ent);
                    return;
                }

                ent.Comp.LastTargetPosition = target.Position;

                Dirty(ent.Owner, ent.Comp);
            }

            forcedDirection = offset.Normalized();
        }
        else
        {
            forcedDirection = currentDirection;
        }
        if (_timing.InSimulation)
        {
            ent.Comp.LastPosition = current.Position;
            Dirty(ent.Owner, ent.Comp);
        }

        var steering = Math.Clamp(ent.Comp.Steering, 0f, 1f);

        var playerWish = args.WishDir;

        var finalDirection = forcedDirection;

        // Normal distance cost is simply how far we moved.
        var distanceCost = traveled;

        if (playerWish != Vector2.Zero && steering > 0f)
        {
            var playerDirection = playerWish.Normalized();

            // Blend the dash direction with player steering.
            var forcedAmount = 1f - steering;

            var blendedDirection =
            forcedDirection * forcedAmount +
            playerDirection * steering;

            if (blendedDirection != Vector2.Zero)
                finalDirection = blendedDirection.Normalized();

            // Only non-homing movement gets the steering distance penalty.
            if (!ent.Comp.Homing)
            {
                // 1  = same direction
                // 0  = 90 degree turn
                // -1 = opposite direction
                var alignment = Vector2.Dot(forcedDirection, playerDirection);

                // Convert that into:
                //
                // same direction = 0
                // 90 degrees     = 0.5
                // opposite       = 1
                var courseChange = (1f - alignment) * 0.5f;

                distanceCost *= 1f + courseChange * steering * ent.Comp.SteeringDistancePenalty;
            }
        }

        // Only change the dash heading during simulation.
        //
        // SharedMoverController also runs client-side between simulation
        // ticks for visual movement. Turning during those extra frames can
        // cause the client trajectory to drift away from the server trajectory.
        if (_timing.InSimulation)
        {
            var maxTurnRadians = MathHelper.DegreesToRadians(ent.Comp.TurnSpeed) * (float)_timing.FrameTime.TotalSeconds;

            finalDirection = RotateTowards(currentDirection, finalDirection, maxTurnRadians);

            ent.Comp.CurrentDirection = finalDirection;

            Dirty(ent.Owner, ent.Comp);
        }
        else
        {
            // Between simulation ticks, continue along the already
            // predicted physical movement direction.
            finalDirection = currentDirection;
        }
        if (_timing.InSimulation)
        {
            ent.Comp.RemainingDistance -= distanceCost;

            Dirty(ent.Owner, ent.Comp);

            if (ent.Comp.RemainingDistance <= ent.Comp.ArrivalDistance)
            {
                StopFullMovement(ent);
                return;
            }
        }

        float distanceRemaining;

        if (ent.Comp.Homing)
        {
            var targetDistanceRemaining = MathF.Max(0f, offset.Length() - ent.Comp.ArrivalDistance);

            var budgetDistanceRemaining = MathF.Max(0f, ent.Comp.RemainingDistance - ent.Comp.ArrivalDistance);

            distanceRemaining = MathF.Min(targetDistanceRemaining, budgetDistanceRemaining);
        }
        else
        {
            distanceRemaining = MathF.Max(0f,
            ent.Comp.RemainingDistance - ent.Comp.ArrivalDistance);
        }

        if (_timing.InSimulation)
        {
            var targetSpeed = ent.Comp.Speed;

            // If deceleration is enabled, calculate the maximum
            // speed from which we can still stop within the
            // remaining distance.
            if (ent.Comp.Deceleration > 0f)
            {
                var brakingSpeed = MathF.Sqrt(2f *
                ent.Comp.Deceleration * distanceRemaining);

                targetSpeed = MathF.Min(
                targetSpeed, brakingSpeed);
            }

            var dt = (float)_timing.FrameTime.TotalSeconds;

            if (ent.Comp.CurrentSpeed < targetSpeed)
            {
                // Accelerate toward target speed.
                if (ent.Comp.Acceleration > 0f)
                {
                    ent.Comp.CurrentSpeed = MathF.Min(
                    targetSpeed, ent.Comp.CurrentSpeed + ent.Comp.Acceleration * dt);
                }
                else
                {
                    ent.Comp.CurrentSpeed = targetSpeed;
                }
            }
            else if (ent.Comp.CurrentSpeed > targetSpeed)
            {
                // Decelerate toward target speed.
                if (ent.Comp.Deceleration > 0f)
                {
                    ent.Comp.CurrentSpeed = MathF.Max(
                    targetSpeed, ent.Comp.CurrentSpeed - ent.Comp.Deceleration * dt);
                }
                else
                {
                    ent.Comp.CurrentSpeed = targetSpeed;
                }
            }

            Dirty(ent.Owner, ent.Comp);
        }

        args.WishDir = finalDirection * ent.Comp.CurrentSpeed;
    }

    private void StopFullMovement(
    Entity<ActiveFullMovementComponent> ent)
    {
        ent.Comp.IsActive = false;
        ent.Comp.CurrentSpeed = 0f;

        Dirty(ent.Owner, ent.Comp);
    }

    /// <summary>
    /// Checks whether the relative movement between the performer
    /// and target entered or crossed the arrival radius during a tick.
    ///
    /// The offsets are targetPosition - performerPosition, so this works
    /// even when both entities are moving.
    /// </summary>
    private static bool PassedArrivalRadius(
    Vector2 previousOffset,
    Vector2 currentOffset,
    float radius)
    {
        var radiusSquared = radius * radius;

        // Already inside the arrival area.
        if (previousOffset.LengthSquared() <= radiusSquared ||
            currentOffset.LengthSquared() <= radiusSquared)
        {
            return true;
        }

        // Relative movement during this simulation tick.
        var movement = currentOffset - previousOffset;

        var movementLengthSquared = movement.LengthSquared();

        if (movementLengthSquared <= 0.000001f)
            return false;

        // Find the closest point to zero along the relative-motion line.
        //
        // Zero represents performer and target occupying the same position.
        var t = Math.Clamp(
        -Vector2.Dot(previousOffset, movement) / movementLengthSquared, 0f, 1f);

        var closestPoint = previousOffset + movement * t;

        return closestPoint.LengthSquared() <= radiusSquared;
    }

    /// <summary>
    /// Rotates one direction toward another without allowing
    /// an instantaneous direction change.
    /// </summary>
    private static Vector2 RotateTowards(
    Vector2 current,
    Vector2 target,
    float maxRadians)
    {
        if (current == Vector2.Zero)
            return target;

        if (target == Vector2.Zero)
            return current;

        current = current.Normalized();
        target = target.Normalized();

        var currentAngle = MathF.Atan2(current.Y, current.X);

        var targetAngle = MathF.Atan2(target.Y, target.X);

        var difference = targetAngle - currentAngle;

        // Convert the difference to the shortest rotation
        // between -PI and +PI.
        difference =
        MathF.Atan2(
            MathF.Sin(difference),
            MathF.Cos(difference));

        var turnAmount = Math.Clamp(
            difference,
            -maxRadians,
            maxRadians);

        var newAngle = currentAngle + turnAmount;

        return new Vector2(
        MathF.Cos(newAngle),
        MathF.Sin(newAngle));
    }
}
