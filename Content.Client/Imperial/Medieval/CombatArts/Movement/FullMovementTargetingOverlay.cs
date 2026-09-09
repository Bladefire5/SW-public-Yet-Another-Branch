/*using System.Numerics;
namespace Content.Shared.Imperial.Medieval.CombatArts.Movement;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client.Imperial.Medieval.CombatArts.Movement;

/// <summary>
/// Shows the complete requested route. Everything after its first forbidden collision or exhausted
/// distance budget stays red, including subsequent waypoints that cannot be reached.
/// </summary>
public sealed class FullMovementTargetingOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly FullMovementTargetingSystem _targeting;
    private readonly SharedTransformSystem _transform;
    private readonly FullMovementCollisionSystem _collision;
    private readonly List<MapCoordinates> _points = new();

    public FullMovementTargetingOverlay(
        FullMovementTargetingSystem targeting,
        SharedTransformSystem transform,
        FullMovementCollisionSystem collision)
    {
        _targeting = targeting;
        _transform = transform;
        _collision = collision;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye == null ||
            !_targeting.TryGetPreview(out var user, out var movement, _points))
            return;

        var start = _transform.GetMapCoordinates(user);
        if (start.MapId != args.MapId || !float.IsFinite(movement.MaxDistance))
            return;

        var remaining = Math.Max(0f, movement.MaxDistance);
        var blocked = false;
        var handle = args.WorldHandle;
        handle.SetTransform(Matrix3x2.Identity);

        foreach (var point in _points)
        {
            if (point.MapId != args.MapId)
                break;

            var offset = point.Position - start.Position;
            var distance = offset.Length();
            if (!float.IsFinite(distance))
                break;

            if (distance > 0.001f)
            {
                var direction = offset / distance;
                var allowed = blocked ? 0f : Math.Min(distance, remaining);
                if (allowed > 0f)
                {
                    allowed = _collision.GetAllowedDistance(user, start, direction, allowed,
                        movement.PassThroughWalls, movement.PassThroughPlayers);
                }

                var reachable = start.Position + direction * allowed;
                if (allowed > 0f)
                    handle.DrawLine(start.Position, reachable, Color.Green);

                if (allowed < distance - 0.001f)
                {
                    blocked = true;
                    handle.DrawLine(reachable, point.Position, Color.Red);
                }

                remaining = Math.Max(0f, remaining - allowed);
            }

            handle.DrawCircle(point.Position, 0.12f, blocked ? Color.Red : Color.Green, false);
            start = point;
        }
    }
}*/
