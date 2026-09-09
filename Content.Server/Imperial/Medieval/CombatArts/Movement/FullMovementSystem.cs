using Content.Shared.Imperial.Medieval.CombatArts.Movement;


namespace Content.Server.Imperial.Medieval.CombatArts.Movement;


public sealed class FullMovementSystem : EntitySystem
{
    [Dependency] private readonly ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("fullmovement");

        SubscribeLocalEvent<FullMovementEvent>(OnFullMovement);
    }

    private void OnFullMovement(FullMovementEvent args)
    {
        if (!TryComp<FullMovementComponent>(args.Action.Owner, out var movement))
            return;

        if (HasComp<ActiveFullMovementComponent>(args.Performer))
            return;

        var active = EnsureComp<ActiveFullMovementComponent>(args.Performer);

        active.Points.Clear();
        active.Points.Add(args.Target);
        active.Speed = movement.Speed;
        active.LockMovement = movement.LockMovement;
        active.DamageImmune = movement.DamageImmune;
        active.PassThroughWalls = movement.PassThroughWalls;
        active.PassThroughMobs = movement.PassThroughMobs;

        active.SourceAction = args.Action.Owner;

        args.Handled = true;

        _sawmill.Info($"FullMovement started by {args.Performer} toward {args.Target}.");

    }


}