using Content.Server.Emp;
using Content.Server.Ghost;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;

namespace Content.Server.Light.EntitySystems;

/// <summary>
///     System for the PoweredLightComponents
/// </summary>
public sealed class PoweredLightSystem : SharedPoweredLightSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PoweredLightComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<PoweredLightComponent, GhostBooEvent>(OnGhostBoo);

        SubscribeLocalEvent<PoweredLightComponent, EmpPulseEvent>(OnEmpPulse);
    }

    private void OnGhostBoo(EntityUid uid, PoweredLightComponent light, GhostBooEvent args)
    {
        if (light.IgnoreGhostsBoo)
            return;

        // check cooldown first to prevent abuse
        var time = GameTiming.CurTime;
        if (light.LastGhostBlink != null)
        {
            if (time <= light.LastGhostBlink + light.GhostBlinkingCooldown)
                return;
        }

        light.LastGhostBlink = time;

        ToggleBlinkingLight(uid, light, true);
        light.GhostBlinkEndTime = time + light.GhostBlinkingTime; //spellward

        args.Handled = true;
    }

    // spellward start
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = GameTiming.CurTime;
        var query = EntityQueryEnumerator<PoweredLightComponent>();
        while (query.MoveNext(out var uid, out var light))
        {
            if (light.GhostBlinkEndTime is not { } end || time < end)
                continue;

            light.GhostBlinkEndTime = null;
            ToggleBlinkingLight(uid, light, false);
        }
    }
    // spellward end

    private void OnMapInit(EntityUid uid, PoweredLightComponent light, MapInitEvent args)
    {
        // TODO: Use ContainerFill dog
        if (light.HasLampOnSpawn != null)
        {
            var entity = EntityManager.SpawnEntity(light.HasLampOnSpawn, EntityManager.GetComponent<TransformComponent>(uid).Coordinates);
            ContainerSystem.Insert(entity, light.LightBulbContainer);
        }
        // need this to update visualizers
        UpdateLight(uid, light);
    }

    private void OnEmpPulse(EntityUid uid, PoweredLightComponent component, ref EmpPulseEvent args)
    {
        if (TryDestroyBulb(uid, component))
            args.Affected = true;
    }
}
