using Content.Server.Spawners.Components;
using Robust.Shared.Timing;
// spellward start
using Content.Server.Imperial.Medieval.Poise;
// spellward end
namespace Content.Server.Spawners.EntitySystems;

public sealed class DelayedSpawnSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    // spellward start
    [Dependency] private readonly PoiseImpactSystem _poiseImpact = default!;
    // spellward end
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DelayedSpawnComponent, MapInitEvent>(OnMapInit);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<DelayedSpawnComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.IsSpawned) continue;
            if (comp.SpawnTime > _timing.CurTime) continue;
            var entity = Spawn(comp.Proto, Transform(uid).Coordinates);
            // spellward start - forward NPC sunstrike poise into its damaging zone.
            _poiseImpact.TransferImpact(uid, entity);
            // spellward end
            if (comp.Attached)
                _transform.SetParent(entity, uid);
            comp.IsSpawned = true;
        }
    }

    private void OnMapInit(EntityUid uid, DelayedSpawnComponent comp, MapInitEvent args)
    {
        if (comp.SpawnTime == TimeSpan.Zero)
            comp.SpawnTime = _timing.CurTime + comp.Delay;
    }
}
