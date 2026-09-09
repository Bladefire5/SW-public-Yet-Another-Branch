/*using Content.Client.Actions;
using Content.Client.UserInterface.Systems.Actions;
using Content.Shared.Actions.Components;
namespace Content.Shared.Imperial.Medieval.CombatArts.Movement;
using Content.Shared.Input;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using static Robust.Shared.Input.Binding.PointerInputCmdHandler;

namespace Content.Client.Imperial.Medieval.CombatArts.Movement;


/// <summary>
/// Isolated targeting for movement skills. Uses the existing action toolbar and magic target-capture
/// binding: middle mouse stores a waypoint, left mouse adds the final point and activates the action,
/// and the action toolbar handles right mouse cancellation.
/// </summary>
public sealed class FullMovementTargetingSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly FullMovementCollisionSystem _collision = default!;

    private readonly List<EntityCoordinates> _points = new();
    private EntityUid? _selectedAction;
    private EntityUid? _selectedUser;

    public override void Initialize()
    {
        base.Initialize();

        // Consume only movement-skill targets before the ordinary single-coordinate action handler.
        SubscribeLocalEvent<FullMovementComponent, ActionTargetAttemptEvent>(OnTargetAttempt,
            before: new[] { typeof(ActionsSystem) });

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ImperialTargetCapture,
                new PointerInputCmdHandler(OnCapturePoint, outsidePrediction: true))
            .Register<FullMovementTargetingSystem>();

        _overlays.AddOverlay(new FullMovementTargetingOverlay(this, _transform, _collision));
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<FullMovementTargetingSystem>();
        _overlays.RemoveOverlay<FullMovementTargetingOverlay>();
        ClearSelection();
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        // Clear captured points on cancellation, action changes, or a change of controlled entity.
        TryGetSelection(out _, out _, out _);
    }

    private bool OnCapturePoint(in PointerInputCmdArgs args)
    {
        if (args.State != BoundKeyState.Down || !_timing.IsFirstTimePredicted ||
            !TryGetSelection(out var user, out var action, out var movement))
            return false;

        if (_actions.GetAction(action) is not { } actionEntity || !_actions.ValidAction(actionEntity))
            return true;

        TryAppendPoint(user, movement, args.Coordinates);
        return true;
    }

    private void OnTargetAttempt(Entity<FullMovementComponent> ent, ref ActionTargetAttemptEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!TryGetSelection(out var user, out var action, out var movement) || action != ent.Owner)
            return;

        // Once all slots are captured, left click confirms those points without replacing the last one.
        TryAppendPoint(user, movement, args.Input.Coordinates);
        if (_points.Count == 0)
            return;

        var points = new List<NetCoordinates>(_points.Count);
        foreach (var point in _points)
            points.Add(GetNetCoordinates(point));

        RaiseNetworkEvent(new FullMovementRequestEvent(GetNetEntity(action), points));
        _points.Clear();
        args.FoundTarget = true;
    }

    private bool TryAppendPoint(EntityUid user, FullMovementComponent movement, EntityCoordinates coordinates)
    {
        if (_points.Count >= movement.MaxPoints || !Exists(coordinates.EntityId))
            return false;

        var map = _transform.ToMapCoordinates(coordinates);
        if (map.MapId != Transform(user).MapID ||
            !float.IsFinite(map.X) || !float.IsFinite(map.Y))
            return false;

        if (_points.Count > 0 &&
            (_transform.ToMapCoordinates(_points[^1]).Position - map.Position).LengthSquared() < 0.0001f)
            return false;

        _points.Add(coordinates);
        return true;
    }

    private bool TryGetSelection(out EntityUid user, out EntityUid action, out FullMovementComponent movement)
    {
        user = default;
        action = default;
        movement = default!;

        var selection = _ui.GetUIController<ActionUIController>().SelectingTargetFor;
        var local = _player.LocalEntity;
        if (selection != _selectedAction || local != _selectedUser)
        {
            _points.Clear();
            _selectedAction = selection;
            _selectedUser = local;
        }

        if (selection is not { } selected || local is not { } performer ||
            !Exists(performer) || !TryComp<FullMovementComponent>(selected, out var config) ||
            !TryComp<ActionComponent>(selected, out var actionComp) || actionComp.AttachedEntity != performer)
        {
            ClearSelection();
            return false;
        }

        foreach (var point in _points)
        {
            if (!Exists(point.EntityId) || _transform.ToMapCoordinates(point).MapId != Transform(performer).MapID)
            {
                _points.Clear();
                break;
            }
        }

        user = performer;
        action = selected;
        movement = config;
        return true;
    }

    private void ClearSelection()
    {
        _points.Clear();
        _selectedAction = null;
        _selectedUser = null;
    }

    /// <summary>Returns the captured path followed by the pending cursor point, in travel order.</summary>
    public bool TryGetPreview(out EntityUid user, out FullMovementComponent movement, List<MapCoordinates> points)
    {
        points.Clear();
        if (!TryGetSelection(out user, out _, out movement) || movement.MaxPoints <= 0)
            return false;

        foreach (var point in _points)
            points.Add(_transform.ToMapCoordinates(point));

        if (points.Count < movement.MaxPoints)
        {
            var cursor = _eye.PixelToMap(_input.MouseScreenPosition.Position);
            if (cursor.MapId == Transform(user).MapID &&
                float.IsFinite(cursor.X) && float.IsFinite(cursor.Y))
                points.Add(cursor);
        }

        return points.Count > 0;
    }
}*/
