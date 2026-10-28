using System.Linq;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Bee.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Pulling.Components;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Bee.Systems;

public sealed partial class MedievalBeeSystem : EntitySystem
{
    private void InitializeHive()
    {
        SubscribeLocalEvent<MedievalBeeHiveComponent, ComponentInit>(HiveInitialize);
        SubscribeLocalEvent<MedievalBeeHiveComponent, InteractHandEvent>(HiveInteract);
        SubscribeLocalEvent<MedievalBeeHiveComponent, ExaminedEvent>(HiveExamined);
        SubscribeLocalEvent<MedievalBeeHiveComponent, DestructionEventArgs>(HiveDestroyed);
        SubscribeLocalEvent<MedievalBeeHiveComponent, ComponentShutdown>(HiveShutdown);
    }
    private void HiveInitialize(EntityUid uid, MedievalBeeHiveComponent component, ComponentInit args)
    {
        if (!_mapLoader.TryLoadMap(new(_random.Pick(_proto.Index(component.GridDataset).Values)), out var createdMap, out var grids))
        {
            Log.Error("failure while initializing bee hive");
            return;
        }
        if (grids.Count > 1)
        {
            Log.Warning("loading bee hive with multiple grids, may cause issues");
        }
        var grid = grids.First();
        var gridComponent = EnsureComp<MedievalBeeGridComponent>(grid);
        component.Grid = (grid, gridComponent);
        component.InteriorMap = createdMap.Value.Owner;
        gridComponent.Hive = uid;
        _mapSystem.InitializeMap((createdMap.Value.Owner, createdMap.Value.Comp));
    }
    private void HiveInteract(EntityUid uid, MedievalBeeHiveComponent component, InteractHandEvent args)
    {
        if (component.InteriorMap == null || component.Grid.Comp.Spawns.Count() <= 0)
        {
            return;
        }
        var spawn = _random.Pick(component.Grid.Comp.Spawns);
        Teleport(args.User, spawn.Owner);
        args.Handled = true;
    }
    private void HiveExamined(EntityUid uid, MedievalBeeHiveComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString($"medieval-bee-pacified-{component.Pacified.ToString().ToLower()}"));
        if (component.PacifyEnd.HasValue)
        {
            args.PushMarkup(Loc.GetString("medieval-bee-smoke-yes", ("time", (component.PacifyEnd.Value - _timing.CurTime).ToString())));
        }
        else
        {
            args.PushMarkup(Loc.GetString("medieval-bee-smoke-no"));
        }
    }
    private void HiveDestroyed(EntityUid uid, MedievalBeeHiveComponent component, DestructionEventArgs args)
        => CleanupHive(uid, component, releaseResidents: true);

    private void HiveShutdown(EntityUid uid, MedievalBeeHiveComponent component, ComponentShutdown args)
        => CleanupHive(uid, component, releaseResidents: false);

    private void CleanupHive(EntityUid uid, MedievalBeeHiveComponent component, bool releaseResidents)
    {
        if (component.InteriorMap is not { } map)
            return;

        component.InteriorMap = null;
        if (TerminatingOrDeleted(map))
            return;

        if (!TerminatingOrDeleted(component.Grid.Owner))
        {
            HashSet<Entity<MobStateComponent>> mobs = new();
            _lookup.GetGridEntities(component.Grid, mobs);
            foreach (var mob in mobs)
            {
                // Direct deletion cleans up the hive's NPCs; visitors and player-controlled residents escape.
                if (!releaseResidents
                    && TryComp<MedievalBeeLinkedMobComponent>(mob, out var linked)
                    && linked.LinkedSpawner is { } spawner
                    && !TerminatingOrDeleted(spawner.Owner)
                    && Transform(spawner).MapUid == map
                    && !(TryComp<MindContainerComponent>(mob, out var mind) && mind.HasMind))
                    continue;

                if (TryComp<PullerComponent>(mob, out var puller)
                    && puller.Pulling is { } target
                    && TryComp<PullableComponent>(target, out var pulled))
                    _pulling.TryStopPull(target, pulled);

                if (TryComp<PullableComponent>(mob, out var pullable))
                    _pulling.TryStopPull(mob, pullable);

                _transform.SetCoordinates(mob, Transform(uid).Coordinates);
                _transform.AttachToGridOrMap(mob);
            }
        }

        QueueDel(map);
    }
    private void UpdateHive(float frameTime)
    {
        var hiveQuery = EntityQueryEnumerator<MedievalBeeHiveComponent>();
        while (hiveQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.PacifyEnd > _timing.CurTime)
                continue;

            comp.PacifyEnd = null;
            if (comp.Pacified)
                UnPacify((uid, comp));

        }
    }
}
