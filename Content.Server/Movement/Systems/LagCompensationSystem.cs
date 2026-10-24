using Content.Server.Movement.Components;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;
// spellward start
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.Prediction;
using Robust.Shared.Enums;
using Robust.Shared.Network;
// spellward end

namespace Content.Server.Movement.Systems;

/// <summary>
/// Stores a buffer of previous positions of the relevant entity.
/// Can be used to check the entity's position at a recent point in time.
/// </summary>
public sealed class LagCompensationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    // spellward start
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private readonly Dictionary<NetUserId, GameTick> _lastRealTicks = new();

    // Maximum supported historical lookup window.
    public static readonly TimeSpan BufferTime = TimeSpan.FromMilliseconds(750);
    // spellward end

    public override void Initialize()
    {
        base.Initialize();
        Log.Level = LogLevel.Info;
        SubscribeLocalEvent<LagCompensationComponent, MoveEvent>(OnLagMove);
        // spellward start
        SubscribeNetworkEvent<MedievalSetLastRealTickEvent>(OnSetLastRealTick);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        // spellward end
    }

    // spellward start
    public override void Shutdown()
    {
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        _lastRealTicks.Clear();
        base.Shutdown();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _lastRealTicks.Clear();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            _lastRealTicks.Remove(args.Session.UserId);
    }

    private void OnSetLastRealTick(MedievalSetLastRealTickEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity == null || !TryGetHistoricalTime(msg.Tick, out _))
            return;

        var userId = args.SenderSession.UserId;

        // A delayed or repeated report must not move this session's view backwards.
        if (_lastRealTicks.TryGetValue(userId, out var previousTick) && msg.Tick <= previousTick)
            return;

        _lastRealTicks[userId] = msg.Tick;
    }

    private bool TryGetHistoricalTime(GameTick tick, out TimeSpan time)
    {
        time = _timing.CurTime;

        // Check before subtracting unsigned tick values.
        if (tick == GameTick.Zero || tick > _timing.CurTick)
            return false;

        var tickOffset = _timing.CurTick.Value - tick.Value;
        var offset = TimeSpan.FromTicks((long) tickOffset * _timing.TickPeriod.Ticks);

        if (offset > BufferTime)
            return false;

        // LastRealTick already identifies the processed state. Do not subtract
        // an additional tick or estimate the client's view from its ping.
        time -= offset;
        return true;
    }
    // spellward end

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var earliestTime = curTime - BufferTime;

        // Cull any old ones from active updates
        // Probably fine to include ignored.
        var query = AllEntityQuery<LagCompensationComponent>();

        while (query.MoveNext(out var comp))
        {
            while (comp.Positions.TryPeek(out var pos))
            {
                if (pos.Item1 < earliestTime)
                {
                    comp.Positions.Dequeue();
                    continue;
                }

                break;
            }
        }
    }

    private void OnLagMove(EntityUid uid, LagCompensationComponent component, ref MoveEvent args)
    {
        if (!args.NewPosition.EntityId.IsValid())
            return; // probably being sent to nullspace for deletion.

        component.Positions.Enqueue((_timing.CurTime, args.NewPosition, args.NewRotation));
    }

    public (EntityCoordinates Coordinates, Angle Angle) GetCoordinatesAngle(EntityUid uid, ICommonSession? pSession,
        TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return (EntityCoordinates.Invalid, Angle.Zero);

        if (pSession == null || !TryComp<LagCompensationComponent>(uid, out var lag) || lag.Positions.Count == 0)
            return (xform.Coordinates, xform.LocalRotation);

        // spellward start
        if (!_lastRealTicks.TryGetValue(pSession.UserId, out var lastRealTick) ||
            !TryGetHistoricalTime(lastRealTick, out var sentTime))
        {
            return (xform.Coordinates, xform.LocalRotation);
        }
        // spellward end

        var angle = Angle.Zero;
        var coordinates = EntityCoordinates.Invalid;

        foreach (var pos in lag.Positions)
        {
            coordinates = pos.Item2;
            angle = pos.Item3;

            if (pos.Item1 >= sentTime)
                break;
        }

        if (coordinates == default)
        {
            Log.Debug($"No long comp coords found, using {xform.Coordinates}");
            coordinates = xform.Coordinates;
            angle = xform.LocalRotation;
        }
        else
        {
            Log.Debug($"Actual coords is {xform.Coordinates} and got {coordinates}");
        }

        return (coordinates, angle);
    }

    public Angle GetAngle(EntityUid uid, ICommonSession? session, TransformComponent? xform = null)
    {
        var (_, angle) = GetCoordinatesAngle(uid, session, xform);
        return angle;
    }

    public EntityCoordinates GetCoordinates(EntityUid uid, ICommonSession? session, TransformComponent? xform = null)
    {
        var (coordinates, _) = GetCoordinatesAngle(uid, session, xform);
        return coordinates;
    }
}
